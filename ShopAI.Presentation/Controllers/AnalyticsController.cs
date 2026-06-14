using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Seller,Admin")]
[Produces("application/json")]
public class AnalyticsController(
    AppDbContext context,
    IUserContext userContext,
    IProductDtoFactory productDtoFactory) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AnalyticsOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview(CancellationToken ct)
    {
        var shopIds = await GetVisibleShopIdsAsync(ct);
        var orderQuery = context.Orders.AsNoTracking().Where(o => shopIds.Contains(o.ShopId));
        var productQuery = context.Products.AsNoTracking().Where(p => shopIds.Contains(p.ShopId));

        var orders = await orderQuery.Include(o => o.Items).ToListAsync(ct);
        var paidStatuses = new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Completed };
        var revenue = orders
            .Where(o => paidStatuses.Contains(o.Status))
            .SelectMany(o => o.Items)
            .Sum(i => i.PriceAtPurchase * i.Quantity);

        var productsCount = await productQuery.CountAsync(ct);
        var inStockProductsCount = await productQuery.CountAsync(p => p.StockQuantity > 0, ct);
        var outOfStockProductsCount = await productQuery.CountAsync(p => p.StockQuantity <= 0, ct);
        var lowStockProductsCount = await productQuery.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= 5, ct);
        var reviewsCount = await context.ProductReviews
            .AsNoTracking()
            .CountAsync(r => shopIds.Contains(r.Product.ShopId), ct);
        var averageRating = await context.ProductReviews
            .AsNoTracking()
            .Where(r => shopIds.Contains(r.Product.ShopId))
            .Select(r => (decimal?)r.Rating)
            .AverageAsync(ct) ?? 0;

        return Ok(new AnalyticsOverviewDto(
            shopIds.Count,
            productsCount,
            inStockProductsCount,
            outOfStockProductsCount,
            lowStockProductsCount,
            orders.Count,
            orders.Count == 0 ? 0 : Math.Round(revenue / orders.Count, 2),
            revenue,
            Math.Round(averageRating, 1),
            reviewsCount));
    }

    [HttpGet("orders/daily")]
    [ProducesResponseType(typeof(List<DailyOrdersAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DailyOrdersAnalyticsDto>>> GetDailyOrders(
        [FromQuery] int days = 14,
        CancellationToken ct = default)
    {
        var safeDays = Math.Clamp(days, 1, 90);
        var shopIds = await GetVisibleShopIdsAsync(ct);
        var from = DateTime.UtcNow.Date.AddDays(-safeDays + 1);

        var orders = await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => shopIds.Contains(o.ShopId) && o.CreatedAt >= from)
            .ToListAsync(ct);

        var paidStatuses = new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Completed };
        var result = orders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new DailyOrdersAnalyticsDto(
                g.Key,
                g.Count(),
                g.Where(o => paidStatuses.Contains(o.Status))
                    .SelectMany(o => o.Items)
                    .Sum(i => i.PriceAtPurchase * i.Quantity)))
            .OrderBy(x => x.Date)
            .ToList();

        return Ok(result);
    }

    [HttpGet("products/top")]
    [ProducesResponseType(typeof(List<ProductShortDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductShortDto>>> GetTopProducts(
        [FromQuery] int limit = 8,
        CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 50);
        var shopIds = await GetVisibleShopIdsAsync(ct);

        var products = await context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .Where(p => shopIds.Contains(p.ShopId) && p.StockQuantity > 0)
            .OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
            .ThenByDescending(p => p.Reviews.Count)
            .ThenByDescending(p => p.Id)
            .Take(safeLimit)
            .ToListAsync(ct);

        return Ok(await productDtoFactory.CreateShortDtosAsync(products, ct));
    }

    private async Task<List<Guid>> GetVisibleShopIdsAsync(CancellationToken ct)
    {
        var query = context.Shops.AsNoTracking();
        if (!userContext.IsAdmin)
        {
            var userId = userContext.UserId;
            query = query.Where(s => s.OwnerId == userId);
        }

        return await query.Select(s => s.Id).ToListAsync(ct);
    }
}

public record AnalyticsOverviewDto(
    int ShopsCount,
    int ProductsCount,
    int InStockProductsCount,
    int OutOfStockProductsCount,
    int LowStockProductsCount,
    int OrdersCount,
    decimal AverageOrderValue,
    decimal Revenue,
    decimal AverageRating,
    int ReviewsCount);

public record DailyOrdersAnalyticsDto(DateTime Date, int OrdersCount, decimal Revenue);
