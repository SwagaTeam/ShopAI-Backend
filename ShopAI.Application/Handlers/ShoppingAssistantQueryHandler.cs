using System.Text;
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
        ["игры"] = ["gaming", "rgb"]
    };

    public async Task<ShoppingAssistantResponse> Handle(ShoppingAssistantQuery request, CancellationToken ct)
    {
        var interpreted = await openRouterClient.InterpretShoppingPromptAsync(request.Request.UserPrompt, ct)
                         ?? BuildFallback(request.Request);

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

        var products = (ranked.Count > 0
                ? ordered.Select(x => x.Product)
                : pool.OrderByDescending(p => p.Id))
            .Take(limit)
            .ToList();

        var items = await productDtoFactory.CreateShortDtosAsync(products, ct);
        var bundles = await BuildBundlesAsync(interpreted, budgetMax, ct);

        return new ShoppingAssistantResponse(interpreted, items, bundles);
    }

    private async Task<List<List<ProductShortDto>>> BuildBundlesAsync(
        InterpretedShoppingQuery interpreted,
        decimal? budgetMax,
        CancellationToken ct)
    {
        var bundles = new List<List<ProductShortDto>>();
        if (!string.Equals(interpreted.Intent, "bundle", StringComparison.OrdinalIgnoreCase))
            return bundles;

        var categories = interpreted.RequiredCategories
            .Concat(interpreted.CategoryHints)
            .SelectMany(Tokenize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (categories.Count == 0) return bundles;

        var pool = await context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => !budgetMax.HasValue || p.Price <= budgetMax.Value)
            .Take(300)
            .ToListAsync(ct);

        var bundle = new List<Domain.Entities.Product>();
        foreach (var category in categories)
        {
            var product = pool
                .Where(p => Contains(p.Category.Name, category) || Contains(p.Name, category) || Contains(p.Tags, category))
                .OrderBy(p => p.Price)
                .FirstOrDefault();

            if (product != null && bundle.All(p => p.Id != product.Id))
                bundle.Add(product);
        }

        if (bundle.Count > 0)
            bundles.Add(await productDtoFactory.CreateShortDtosAsync(bundle, ct));

        return bundles;
    }

    private static int ScoreProduct(
        Domain.Entities.Product product,
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

        foreach (var color in interpreted.Colors.Where(c => !string.IsNullOrWhiteSpace(c)))
            if (Contains(product.AttributesJson, color) || Contains(product.Tags, color)) score += 4;

        foreach (var attr in interpreted.Attributes)
            if (Contains(product.AttributesJson, attr.Key) && Contains(product.AttributesJson, attr.Value)) score += 5;

        return score;
    }

    private static int SoftScoreProduct(Domain.Entities.Product product, IReadOnlyCollection<string> terms)
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
            if (Synonyms.TryGetValue(term, out var synonyms))
                terms.AddRange(synonyms);
        }

        return terms
            .Where(t => t.Length > 1)
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
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

    private static bool Contains(string? source, string value)
        => !string.IsNullOrWhiteSpace(source)
           && source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static InterpretedShoppingQuery BuildFallback(ShoppingAssistantRequest request)
    {
        return new InterpretedShoppingQuery
        {
            Intent = "search",
            Keywords = Tokenize(request.UserPrompt).Take(10).ToList(),
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            PriceSort = "none"
        };
    }
}
