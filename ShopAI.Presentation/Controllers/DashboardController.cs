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
[Authorize(Roles = "User,Seller,Admin")]
public class DashboardController(
    AppDbContext context,
    IUserContext userContext,
    IProductDtoFactory productDtoFactory) : ControllerBase
{
    /// <summary>
    /// Получить дашборд покупателя для текущего пользователя.
    /// </summary>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Краткая статистика пользователя и подборка популярных товаров.</returns>
    /// <response code="200">Дашборд покупателя успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CustomerDashboardVm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
            .Where(p => p.StockQuantity > 0)
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

    /// <summary>
    /// Получить дашборд продавца для текущего пользователя.
    /// </summary>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Статистика магазинов продавца и топ товаров по рейтингу.</returns>
    /// <response code="200">Дашборд продавца успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Пользователь не имеет роли Seller или Admin.</response>
    [HttpGet("seller")]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(typeof(SellerDashboardVm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            .Where(p => shopIds.Contains(p.ShopId) && p.StockQuantity > 0)
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

/// <summary>
/// Краткая статистика покупателя.
/// </summary>
/// <param name="WishlistCount">Количество товаров в избранном.</param>
/// <param name="CartItemsCount">Общее количество единиц товара в корзине.</param>
/// <param name="CartTotal">Текущая сумма корзины.</param>
/// <param name="RecentlyViewedCount">Количество недавно просмотренных товаров.</param>
/// <param name="ReviewsCount">Количество отзывов текущего пользователя.</param>
public record CustomerMiniStatsVm(
    int WishlistCount,
    int CartItemsCount,
    decimal CartTotal,
    int RecentlyViewedCount,
    int ReviewsCount);

/// <summary>
/// Данные дашборда покупателя.
/// </summary>
/// <param name="Stats">Краткая статистика покупателя.</param>
/// <param name="Popular">Популярные товары для отображения на дашборде.</param>
public record CustomerDashboardVm(CustomerMiniStatsVm Stats, List<ProductShortDto> Popular);

/// <summary>
/// Краткая статистика продавца.
/// </summary>
/// <param name="ShopsCount">Количество магазинов продавца.</param>
/// <param name="ProductsCount">Количество товаров во всех магазинах продавца.</param>
/// <param name="StockQuantity">Суммарный остаток товаров на складе.</param>
/// <param name="LowStockProducts">Количество товаров с низким остатком.</param>
/// <param name="OrdersCount">Количество заказов по магазинам продавца.</param>
/// <param name="Revenue">Выручка по оплаченным и выполненным заказам.</param>
/// <param name="AverageRating">Средний рейтинг товаров продавца.</param>
/// <param name="ReviewsCount">Количество отзывов на товары продавца.</param>
public record SellerStatsVm(
    int ShopsCount,
    int ProductsCount,
    int StockQuantity,
    int LowStockProducts,
    int OrdersCount,
    decimal Revenue,
    decimal AverageRating,
    int ReviewsCount);

/// <summary>
/// Данные дашборда продавца.
/// </summary>
/// <param name="Stats">Краткая статистика продавца.</param>
/// <param name="TopProducts">Топ товаров продавца по рейтингу и отзывам.</param>
public record SellerDashboardVm(SellerStatsVm Stats, List<ProductShortDto> TopProducts);
