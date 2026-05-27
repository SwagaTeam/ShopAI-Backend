using AutoMapper;
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
    IMapper mapper)
    : IRequestHandler<ShoppingAssistantQuery, ShoppingAssistantResponse>
{
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

        foreach (var kw in interpreted.Keywords.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var k = kw.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(k) ||
                p.Description.ToLower().Contains(k) ||
                p.Tags.ToLower().Contains(k));
        }

        foreach (var br in interpreted.Brands.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var b = br.ToLower();
            query = query.Where(p => p.Brand != null && p.Brand.Name.ToLower().Contains(b));
        }

        foreach (var color in interpreted.Colors.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var c = color.ToLower();
            query = query.Where(p => p.AttributesJson.ToLower().Contains(c) || p.Tags.ToLower().Contains(c));
        }

        foreach (var tag in interpreted.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var t = tag.ToLower();
            query = query.Where(p => p.Tags.ToLower().Contains(t));
        }

        foreach (var attr in interpreted.Attributes)
        {
            var marker = $"\"{attr.Key}\":\"{attr.Value}\"".ToLower();
            query = query.Where(p => p.AttributesJson.ToLower().Contains(marker));
        }

        query = interpreted.PriceSort switch
        {
            "asc" => query.OrderBy(p => p.Price),
            "desc" => query.OrderByDescending(p => p.Price),
            _ => query.OrderByDescending(p => p.Id)
        };

        var limit = Math.Clamp(request.Request.Limit ?? 20, 1, 100);
        var products = await query.Take(limit).ToListAsync(ct);
        var items = mapper.Map<List<ProductShortDto>>(products);

        var bundles = new List<List<ProductShortDto>>();
        if (interpreted.Intent == "bundle")
        {
            var categories = interpreted.RequiredCategories.Select(x => x.ToLower()).Distinct().ToList();
            if (categories.Count > 0)
            {
                var pool = await context.Products.AsNoTracking().Include(p => p.Shop).Include(p => p.Brand).Include(p => p.Category)
                    .Where(p => categories.Any(c => p.Category.Name.ToLower().Contains(c) || p.Name.ToLower().Contains(c)))
                    .Take(200).ToListAsync(ct);

                var grouped = pool.GroupBy(p => categories.FirstOrDefault(c => p.Category.Name.ToLower().Contains(c) || p.Name.ToLower().Contains(c)) ?? "other")
                    .ToDictionary(g => g.Key, g => g.ToList());

                var bundle = new List<ProductShortDto>();
                foreach (var cat in categories)
                {
                    if (grouped.TryGetValue(cat, out var list) && list.Count > 0)
                    {
                        bundle.Add(mapper.Map<ProductShortDto>(list[0]));
                    }
                }

                if (bundle.Count > 0) bundles.Add(bundle);
            }
        }

        return new ShoppingAssistantResponse(interpreted, items, bundles);
    }

    private static InterpretedShoppingQuery BuildFallback(ShoppingAssistantRequest request)
    {
        var words = request.UserPrompt.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLower()).Distinct().Take(10).ToList();

        return new InterpretedShoppingQuery
        {
            Intent = "search",
            Keywords = words,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            PriceSort = "none"
        };
    }
}
