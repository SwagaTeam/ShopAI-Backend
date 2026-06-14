using System.Text;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;

namespace ShopAI.Application.Handlers;

public record ShoppingAssistantQuery(ShoppingAssistantRequest Request) : IRequest<ShoppingAssistantResponse>;

public class ShoppingAssistantQueryHandler(
    AppDbContext context,
    IOpenRouterClient openRouterClient,
    IProductDtoFactory productDtoFactory)
    : IRequestHandler<ShoppingAssistantQuery, ShoppingAssistantResponse>
{
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["телефон"] = ["smartphone", "phone", "смартфон"],
        ["смартфон"] = ["smartphone", "phone", "телефон"],
        ["ноутбук"] = ["laptop", "ultrabook"],
        ["наушники"] = ["headphones", "earbuds", "audio"],
        ["клавиатура"] = ["keyboard", "gaming"],
        ["кофе"] = ["coffee", "espresso", "barista"],
        ["чайник"] = ["kettle", "coffee"],
        ["пылесос"] = ["vacuum", "cleaning"],
        ["лампа"] = ["lamp", "lighting", "led"],
        ["кроссовки"] = ["sneakers", "running"],
        ["рюкзак"] = ["backpack", "city"],
        ["часы"] = ["watch", "smartwatch"],
        ["куртка"] = ["jacket", "outerwear"],
        ["гантели"] = ["dumbbells", "fitness"],
        ["велосипед"] = ["cycling", "helmet"],
        ["палатка"] = ["tent", "camping"],
        ["массаж"] = ["massage", "recovery"],
        ["спорт"] = ["sport", "fitness"],
        ["дом"] = ["home", "kitchen", "smart-home"],
        ["кухня"] = ["kitchen", "home", "interior"],
        ["шкаф"] = ["cabinet", "kitchen-cabinet", "storage"],
        ["стул"] = ["chair", "dining-chair"],
        ["стол"] = ["table", "dining-table"],
        ["плитка"] = ["tile", "backsplash", "ceramic"],
        ["холодильник"] = ["fridge", "refrigerator"],
        ["серый"] = ["gray", "grey", "graphite", "silver", "steel"]
    };

    private static readonly BundleSlot[] KitchenSlots =
    [
        new("cabinet", "Кухонный шкаф", ["шкаф", "шкафы", "гарнитур", "cabinet", "kitchen cabinet", "kitchen-cabinet", "storage"]),
        new("chair", "Стул", ["стул", "стулья", "chair", "chairs", "dining-chair"]),
        new("table", "Стол", ["стол", "table", "dining table", "dining-table"]),
        new("tile", "Плитка", ["плитка", "кафель", "керамогранит", "tile", "tiles", "backsplash", "ceramic"]),
        new("fridge", "Холодильник", ["холодильник", "fridge", "refrigerator"])
    ];

    public async Task<ShoppingAssistantResponse> Handle(ShoppingAssistantQuery request, CancellationToken ct)
    {
        var interpreted = await openRouterClient.InterpretShoppingPromptAsync(request.Request.UserPrompt, ct)
                         ?? BuildFallback(request.Request);

        interpreted = EnrichFallbackHints(request.Request.UserPrompt, interpreted, request.Request);

        var query = context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .AsQueryable();

        if (request.Request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.Request.CategoryId.Value);

        var budgetMin = request.Request.BudgetMin ?? interpreted.BudgetMin;
        var budgetMax = request.Request.BudgetMax ?? interpreted.BudgetMax;
        if (budgetMin.HasValue) query = query.Where(p => p.Price >= budgetMin.Value);
        if (budgetMax.HasValue) query = query.Where(p => p.Price <= budgetMax.Value);

        var limit = Math.Clamp(request.Request.Limit ?? 20, 1, 100);
        var pool = await query
            .OrderByDescending(p => p.Id)
            .Take(500)
            .ToListAsync(ct);

        var terms = BuildSearchTerms(request.Request.UserPrompt, interpreted);
        var ranked = pool
            .Select(p => new { Product = p, Score = ScoreProduct(p, terms, interpreted) })
            .Where(x => terms.Count == 0 || x.Score > 0)
            .ToList();

        if (ranked.Count == 0 && terms.Count > 0)
        {
            ranked = pool
                .Select(p => new { Product = p, Score = SoftScoreProduct(p, terms) })
                .Where(x => x.Score > 0)
                .ToList();
        }

        var ordered = interpreted.PriceSort switch
        {
            "asc" => ranked.OrderBy(x => x.Product.Price).ThenByDescending(x => x.Score),
            "desc" => ranked.OrderByDescending(x => x.Product.Price).ThenByDescending(x => x.Score),
            _ => ranked.OrderByDescending(x => x.Score).ThenByDescending(x => x.Product.Id)
        };

        var products = ranked.Count > 0
            ? ordered.Select(x => x.Product).Take(limit).ToList()
            : new List<Product>();

        var items = await productDtoFactory.CreateShortDtosAsync(products, ct);
        var bundles = await BuildBundlesAsync(request.Request.UserPrompt, interpreted, budgetMin, budgetMax, ct);

        return new ShoppingAssistantResponse(interpreted, items, bundles);
    }

    private async Task<List<List<ProductShortDto>>> BuildBundlesAsync(
        string prompt,
        InterpretedShoppingQuery interpreted,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken ct)
    {
        var slots = ResolveBundleSlots(prompt, interpreted);
        if (slots.Count == 0) return [];

        var query = context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => !budgetMin.HasValue || p.Price >= budgetMin.Value)
            .Where(p => !budgetMax.HasValue || p.Price <= budgetMax.Value);

        var pool = await query.Take(700).ToListAsync(ct);
        var colorTerms = ResolveColorTerms(prompt, interpreted);
        var bundles = BuildProductBundles(pool, slots, colorTerms);

        var result = new List<List<ProductShortDto>>();
        foreach (var bundle in bundles)
        {
            result.Add(await productDtoFactory.CreateShortDtosAsync(bundle, ct));
        }

        return result;
    }

    private static List<List<Product>> BuildProductBundles(
        IReadOnlyCollection<Product> pool,
        IReadOnlyList<BundleSlot> slots,
        IReadOnlyCollection<string> colorTerms)
    {
        var candidatesBySlot = slots
            .Select(slot => new
            {
                Slot = slot,
                Products = pool
                    .Select(product => new
                    {
                        Product = product,
                        RoleScore = ScoreSlot(product, slot),
                        ColorScore = ScoreColor(product, colorTerms)
                    })
                    .Where(x => x.RoleScore > 0)
                    .OrderByDescending(x => x.ColorScore)
                    .ThenByDescending(x => x.RoleScore)
                    .ThenBy(x => x.Product.Price)
                    .Select(x => x.Product)
                    .DistinctBy(p => p.Id)
                    .Take(8)
                    .ToList()
            })
            .ToList();

        if (candidatesBySlot.Any(x => x.Products.Count == 0))
            return [];

        var variantCount = Math.Min(4, candidatesBySlot.Max(x => x.Products.Count));
        var variants = new List<List<Product>>();
        var seen = new HashSet<string>();

        for (var variantIndex = 0; variantIndex < variantCount; variantIndex++)
        {
            var used = new HashSet<Guid>();
            var variant = new List<Product>();

            for (var slotIndex = 0; slotIndex < candidatesBySlot.Count; slotIndex++)
            {
                var candidates = candidatesBySlot[slotIndex].Products;
                var product = PickCandidate(candidates, variantIndex + slotIndex, used);
                if (product == null) break;

                used.Add(product.Id);
                variant.Add(product);
            }

            if (variant.Count != slots.Count) continue;

            var signature = string.Join('|', variant.Select(p => p.Id).OrderBy(id => id));
            if (seen.Add(signature))
                variants.Add(variant);
        }

        return variants;
    }

    private static Product? PickCandidate(IReadOnlyList<Product> candidates, int startIndex, HashSet<Guid> used)
    {
        for (var offset = 0; offset < candidates.Count; offset++)
        {
            var candidate = candidates[(startIndex + offset) % candidates.Count];
            if (!used.Contains(candidate.Id)) return candidate;
        }

        return null;
    }

    private static List<BundleSlot> ResolveBundleSlots(string prompt, InterpretedShoppingQuery interpreted)
    {
        var allTerms = BuildSearchTerms(prompt, interpreted);
        var asksForBundle = string.Equals(interpreted.Intent, "bundle", StringComparison.OrdinalIgnoreCase)
                            || HasAnyRoot(allTerms, "вариант", "комплект", "набор", "кух", "interior", "kitchen");

        if (!asksForBundle) return [];

        if (HasAnyRoot(allTerms, "кух", "kitchen"))
            return KitchenSlots.ToList();

        var slotTerms = interpreted.RequiredCategories
            .Concat(interpreted.CategoryHints)
            .SelectMany(Tokenize)
            .Where(t => t.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(term => new BundleSlot(term, term, ExpandTerm(term).ToArray()))
            .ToList();

        return slotTerms.Count > 1 ? slotTerms : [];
    }

    private static int ScoreProduct(
        Product product,
        IReadOnlyCollection<string> terms,
        InterpretedShoppingQuery interpreted)
    {
        var score = 0;
        foreach (var term in terms)
        {
            if (Contains(product.Name, term)) score += 8;
            if (Contains(product.Brand?.Name, term)) score += 7;
            if (Contains(product.Category?.Name, term)) score += 6;
            if (Contains(product.Tags, term)) score += 5;
            if (Contains(product.Description, term)) score += 3;
            if (Contains(product.AttributesJson, term)) score += 2;
            if (product.Price.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)) score += 1;
        }

        foreach (var brand in interpreted.Brands.Where(b => !string.IsNullOrWhiteSpace(b)))
            if (Contains(product.Brand?.Name, brand)) score += 10;

        foreach (var color in interpreted.Colors.Where(c => !string.IsNullOrWhiteSpace(c)).SelectMany(ExpandTerm))
            if (Contains(product.AttributesJson, color) || Contains(product.Tags, color) || Contains(product.Name, color))
                score += 4;

        foreach (var attr in interpreted.Attributes)
            if (Contains(product.AttributesJson, attr.Key) && Contains(product.AttributesJson, attr.Value)) score += 5;

        return score;
    }

    private static int ScoreSlot(Product product, BundleSlot slot)
    {
        var score = 0;
        foreach (var term in slot.Terms.SelectMany(ExpandTerm).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (ContainsSlotTerm(product.Name, term)) score += 9;
            if (ContainsSlotTerm(product.Category?.Name, term)) score += 7;
            if (ContainsSlotTerm(product.Tags, term)) score += 6;
            if (ContainsSlotTerm(product.Description, term)) score += 3;
            if (ContainsSlotTerm(product.AttributesJson, term)) score += 2;
        }

        return score;
    }

    private static int ScoreColor(Product product, IReadOnlyCollection<string> colorTerms)
    {
        if (colorTerms.Count == 0) return 0;

        var score = 0;
        foreach (var term in colorTerms)
        {
            if (Contains(product.AttributesJson, term)) score += 8;
            if (Contains(product.Tags, term)) score += 6;
            if (Contains(product.Name, term)) score += 4;
            if (Contains(product.Description, term)) score += 2;
        }

        return score;
    }

    private static int SoftScoreProduct(Product product, IReadOnlyCollection<string> terms)
    {
        var haystack = $"{product.Name} {product.Description} {product.Tags} {product.AttributesJson} {product.Brand?.Name} {product.Category?.Name}";
        return terms.Count(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> BuildSearchTerms(string prompt, InterpretedShoppingQuery interpreted)
    {
        var terms = new List<string>();
        terms.AddRange(Tokenize(prompt));
        terms.AddRange(interpreted.Keywords.SelectMany(Tokenize));
        terms.AddRange(interpreted.Tags.SelectMany(Tokenize));
        terms.AddRange(interpreted.CategoryHints.SelectMany(Tokenize));
        terms.AddRange(interpreted.RequiredCategories.SelectMany(Tokenize));
        terms.AddRange(interpreted.Brands.SelectMany(Tokenize));
        terms.AddRange(interpreted.Colors.SelectMany(Tokenize));
        terms.AddRange(interpreted.Attributes.Keys.SelectMany(Tokenize));
        terms.AddRange(interpreted.Attributes.Values.SelectMany(Tokenize));

        foreach (var term in terms.ToList())
        {
            terms.AddRange(ExpandTerm(term));
        }

        return terms
            .Where(t => t.Length > 1)
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();
    }

    private static List<string> ResolveColorTerms(string prompt, InterpretedShoppingQuery interpreted)
    {
        var terms = Tokenize(prompt)
            .Concat(interpreted.Colors.SelectMany(Tokenize))
            .Concat(interpreted.Tags.SelectMany(Tokenize))
            .SelectMany(ExpandTerm)
            .Where(IsColorTerm)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return terms;
    }

    private static IEnumerable<string> ExpandTerm(string term)
    {
        if (Synonyms.TryGetValue(term, out var synonyms))
        {
            yield return term;
            foreach (var synonym in synonyms) yield return synonym;
            yield break;
        }

        yield return term;

        if (term.StartsWith("сер", StringComparison.OrdinalIgnoreCase))
        {
            yield return "серый";
            yield return "gray";
            yield return "grey";
            yield return "graphite";
            yield return "silver";
            yield return "steel";
        }

        if (term.StartsWith("кух", StringComparison.OrdinalIgnoreCase))
        {
            yield return "кухня";
            yield return "kitchen";
            yield return "home";
            yield return "interior";
        }

        if (term.StartsWith("шкаф", StringComparison.OrdinalIgnoreCase))
        {
            yield return "cabinet";
            yield return "kitchen-cabinet";
            yield return "storage";
        }

        if (term.StartsWith("стул", StringComparison.OrdinalIgnoreCase))
        {
            yield return "chair";
            yield return "dining-chair";
        }

        if (term.StartsWith("стол", StringComparison.OrdinalIgnoreCase))
        {
            yield return "table";
            yield return "dining-table";
        }

        if (term.StartsWith("плит", StringComparison.OrdinalIgnoreCase))
        {
            yield return "tile";
            yield return "backsplash";
            yield return "ceramic";
        }

        if (term.StartsWith("холод", StringComparison.OrdinalIgnoreCase))
        {
            yield return "fridge";
            yield return "refrigerator";
        }
    }

    private static InterpretedShoppingQuery EnrichFallbackHints(
        string prompt,
        InterpretedShoppingQuery interpreted,
        ShoppingAssistantRequest request)
    {
        var terms = BuildSearchTerms(prompt, interpreted);
        var isKitchenBundle = HasAnyRoot(terms, "кух", "kitchen")
                              && HasAnyRoot(terms, "вариант", "комплект", "набор", "кух", "kitchen");

        if (!isKitchenBundle) return interpreted;

        var colors = interpreted.Colors
            .Concat(ResolveColorTerms(prompt, interpreted))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new InterpretedShoppingQuery
        {
            Intent = "bundle",
            CategoryHints = interpreted.CategoryHints.Count > 0 ? interpreted.CategoryHints : ["kitchen"],
            RequiredCategories = KitchenSlots.Select(s => s.Key).ToList(),
            Keywords = interpreted.Keywords,
            Colors = colors,
            Brands = interpreted.Brands,
            Tags = interpreted.Tags,
            Attributes = interpreted.Attributes,
            BudgetMin = request.BudgetMin ?? interpreted.BudgetMin,
            BudgetMax = request.BudgetMax ?? interpreted.BudgetMax,
            PriceSort = interpreted.PriceSort,
            BundleSize = KitchenSlots.Length
        };
    }

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-')
            {
                current.Append(char.ToLowerInvariant(ch));
                continue;
            }

            Flush();
        }

        Flush();
        return result;

        void Flush()
        {
            if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
        }
    }

    private static bool HasAnyRoot(IEnumerable<string> terms, params string[] roots)
        => terms.Any(term => roots.Any(root => term.StartsWith(root, StringComparison.OrdinalIgnoreCase)));

    private static bool IsColorTerm(string term)
        => term.StartsWith("сер", StringComparison.OrdinalIgnoreCase)
           || term is "gray" or "grey" or "graphite" or "silver" or "steel";

    private static bool Contains(string? source, string value)
        => !string.IsNullOrWhiteSpace(source)
           && source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsSlotTerm(string? source, string term)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;

        if (term.Contains(' ', StringComparison.Ordinal) || term.Contains('-', StringComparison.Ordinal))
            return source.Contains(term, StringComparison.OrdinalIgnoreCase);

        return Tokenize(source).Any(token =>
            token.Equals(term, StringComparison.OrdinalIgnoreCase)
            || token.StartsWith(term, StringComparison.OrdinalIgnoreCase));
    }

    private static InterpretedShoppingQuery BuildFallback(ShoppingAssistantRequest request)
    {
        return new InterpretedShoppingQuery
        {
            Intent = HasAnyRoot(Tokenize(request.UserPrompt), "вариант", "комплект", "набор", "кух") ? "bundle" : "search",
            Keywords = Tokenize(request.UserPrompt).Take(10).ToList(),
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            PriceSort = "none"
        };
    }

    private sealed record BundleSlot(string Key, string Name, string[] Terms);
}
