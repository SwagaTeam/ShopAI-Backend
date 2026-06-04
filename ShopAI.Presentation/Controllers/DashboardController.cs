using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Admin")]
public class DashboardController(
    AppDbContext context,
    IUserContext userContext,
    IProductDtoFactory productDtoFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CustomerDashboardVm>> GetCustomerDashboard(CancellationToken ct)
    {
        var userId = userContext.UserId;

        var cartItems = await context.Set<CartItem>()
            .AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .Select(i => new { i.Quantity, i.Product.Price })
            .ToListAsync(ct);

        var wishlistCount = await context.Set<FavoriteProduct>()
            .AsNoTracking()
            .CountAsync(f => f.UserId == userId, ct);

        var recentlyViewedCount = await context.Set<RecentlyViewedProduct>()
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId, ct);

        var reviewsCount = await context.ProductReviews
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId, ct);

        var popularProducts = await context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
            .ThenByDescending(p => p.Reviews.Count)
            .ThenByDescending(p => p.StockQuantity)
            .Take(8)
            .ToListAsync(ct);

        var popular = await productDtoFactory.CreateShortDtosAsync(popularProducts, ct);
        var stats = new CustomerMiniStatsVm(
            wishlistCount,
            cartItems.Sum(i => i.Quantity),
            cartItems.Sum(i => i.Price * i.Quantity),
            recentlyViewedCount,
            reviewsCount);

        return Ok(new CustomerDashboardVm(stats, popular));
    }

    [HttpGet("seller")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SellerDashboardVm>> GetSellerDashboard(CancellationToken ct)
    {
        var userId = userContext.UserId;

        var shopIds = await context.Shops
            .AsNoTracking()
            .Where(s => s.OwnerId == userId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var totalProducts = await context.Products
            .AsNoTracking()
            .CountAsync(p => shopIds.Contains(p.ShopId), ct);

        var totalStock = await context.Products
            .AsNoTracking()
            .Where(p => shopIds.Contains(p.ShopId))
            .SumAsync(p => p.StockQuantity, ct);

        var lowStockProducts = await context.Products
            .AsNoTracking()
            .CountAsync(p => shopIds.Contains(p.ShopId) && p.StockQuantity <= 5, ct);

        var totalReviews = await context.ProductReviews
            .AsNoTracking()
            .CountAsync(r => shopIds.Contains(r.Product.ShopId), ct);

        var averageRating = await context.ProductReviews
            .AsNoTracking()
            .Where(r => shopIds.Contains(r.Product.ShopId))
            .Select(r => (decimal?)r.Rating)
            .AverageAsync(ct) ?? 0;

        var orders = await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => shopIds.Contains(o.ShopId))
            .ToListAsync(ct);

        var revenue = orders
            .Where(o => o.Status is Domain.Enums.OrderStatus.Processing or Domain.Enums.OrderStatus.Shipped or Domain.Enums.OrderStatus.Completed)
            .SelectMany(o => o.Items)
            .Sum(i => i.PriceAtPurchase * i.Quantity);

        var topProducts = await context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .Where(p => shopIds.Contains(p.ShopId))
            .OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
            .ThenByDescending(p => p.Reviews.Count)
            .Take(8)
            .ToListAsync(ct);

        var stats = new SellerStatsVm(
            shopIds.Count,
            totalProducts,
            totalStock,
            lowStockProducts,
            orders.Count,
            revenue,
            Math.Round(averageRating, 1),
            totalReviews);

        return Ok(new SellerDashboardVm(stats, await productDtoFactory.CreateShortDtosAsync(topProducts, ct)));
    }
}

public record CustomerMiniStatsVm(
    int WishlistCount,
    int CartItemsCount,
    decimal CartTotal,
    int RecentlyViewedCount,
    int ReviewsCount);

public record CustomerDashboardVm(CustomerMiniStatsVm Stats, List<ProductShortDto> Popular);

public record SellerStatsVm(
    int ShopsCount,
    int ProductsCount,
    int StockQuantity,
    int LowStockProducts,
    int OrdersCount,
    decimal Revenue,
    decimal AverageRating,
    int ReviewsCount);

public record SellerDashboardVm(SellerStatsVm Stats, List<ProductShortDto> TopProducts);
