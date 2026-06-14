using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Seller,Admin")]
public class OrdersController(
    AppDbContext context,
    IUserContext userContext,
    IFileStorageService fileStorageService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Получить список заказов текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает только заказы авторизованного пользователя. Для демонстрационного сценария статус заказа может автоматически продвинуться по времени.
    /// </remarks>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Список заказов пользователя от новых к старым.</returns>
    /// <response code="200">Список заказов успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<OrderDto>>> GetMy(CancellationToken ct)
    {
        var userId = userContext.UserId;
        var orders = await context.Orders
            .Include(o => o.Shop)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        var changed = false;
        foreach (var order in orders)
        {
            changed |= ApplyDemoStatusProgression(order);
        }

        if (changed)
            await context.SaveChangesAsync(ct);

        var result = new List<OrderDto>(orders.Count);
        foreach (var order in orders)
        {
            result.Add(await ToDtoAsync(order));
        }

        return Ok(result);
    }

    /// <summary>
    /// Получить один заказ текущего пользователя по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор заказа.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Детальная информация о заказе с позициями.</returns>
    /// <response code="200">Заказ найден и возвращен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Заказ не найден или принадлежит другому пользователю.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = userContext.UserId;
        var order = await context.Orders
            .Include(o => o.Shop)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct);

        if (order == null) return NotFound();

        if (ApplyDemoStatusProgression(order))
            await context.SaveChangesAsync(ct);

        return Ok(await ToDtoAsync(order));
    }

    private static bool ApplyDemoStatusProgression(Order order)
    {
        if (order.Status == OrderStatus.Cancelled)
            return false;

        var start = order.PaidAtUtc ?? order.CreatedAt;
        var elapsed = DateTime.UtcNow - start;
        var next = elapsed.TotalMinutes switch
        {
            >= 5 => OrderStatus.Completed,
            >= 3 => OrderStatus.Shipped,
            >= 1 => OrderStatus.Processing,
            _ => order.Status
        };

        if (order.Status == next)
            return false;

        order.Status = next;
        return true;
    }

    private async Task<OrderDto> ToDtoAsync(Order order)
    {
        var items = new List<OrderItemDto>(order.Items.Count);
        foreach (var item in order.Items)
        {
            items.Add(new OrderItemDto(
                item.ProductId,
                item.Product?.Name ?? "Товар",
                await ResolveImageUrlAsync(item.Product?.ImageUrl),
                item.Quantity,
                item.PriceAtPurchase,
                item.TotalPrice));
        }

        return new OrderDto(
            order.Id,
            order.ShopId,
            order.Shop?.Name ?? "Магазин",
            order.CreatedAt,
            order.Status.ToString(),
            ToStatusLabel(order.Status),
            order.PaymentStatus ?? string.Empty,
            order.DeliveryAddress,
            order.ContactPhone,
            order.Comment,
            items.Sum(i => i.TotalPrice),
            items);
    }

    private async Task<string> ResolveImageUrlAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;

        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        return await fileStorageService.GetPresignedUrlAsync(bucket, value);
    }

    private static OrderDto ToDto(Order order)
    {
        var items = order.Items.Select(i => new OrderItemDto(
            i.ProductId,
            i.Product?.Name ?? "Товар",
            i.Product?.ImageUrl ?? string.Empty,
            i.Quantity,
            i.PriceAtPurchase,
            i.TotalPrice)).ToList();

        return new OrderDto(
            order.Id,
            order.ShopId,
            order.Shop?.Name ?? "Магазин",
            order.CreatedAt,
            order.Status.ToString(),
            ToStatusLabel(order.Status),
            order.PaymentStatus ?? string.Empty,
            order.DeliveryAddress,
            order.ContactPhone,
            order.Comment,
            items.Sum(i => i.TotalPrice),
            items);
    }

    private static string ToStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.New => "Новый",
        OrderStatus.Processing => "В обработке",
        OrderStatus.Shipped => "Отправлен",
        OrderStatus.Completed => "Завершен",
        OrderStatus.Cancelled => "Отменен",
        _ => status.ToString()
    };
}

/// <summary>
/// Информация о заказе пользователя.
/// </summary>
/// <param name="Id">Идентификатор заказа.</param>
/// <param name="ShopId">Идентификатор магазина, в котором создан заказ.</param>
/// <param name="ShopName">Название магазина.</param>
/// <param name="CreatedAt">Дата и время создания заказа.</param>
/// <param name="Status">Технический статус заказа.</param>
/// <param name="StatusLabel">Человекочитаемая подпись статуса.</param>
/// <param name="PaymentStatus">Статус оплаты у платежного провайдера.</param>
/// <param name="DeliveryAddress">Адрес доставки.</param>
/// <param name="ContactPhone">Контактный телефон получателя.</param>
/// <param name="Comment">Комментарий к заказу.</param>
/// <param name="TotalPrice">Итоговая стоимость заказа.</param>
/// <param name="Items">Позиции заказа.</param>
public record OrderDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    DateTime CreatedAt,
    string Status,
    string StatusLabel,
    string PaymentStatus,
    string DeliveryAddress,
    string ContactPhone,
    string? Comment,
    decimal TotalPrice,
    List<OrderItemDto> Items);

/// <summary>
/// Позиция заказа.
/// </summary>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="ProductName">Название товара на момент выдачи ответа.</param>
/// <param name="ImageUrl">Ссылка или путь к изображению товара.</param>
/// <param name="Quantity">Количество единиц товара в заказе.</param>
/// <param name="Price">Цена одной единицы товара на момент покупки.</param>
/// <param name="TotalPrice">Стоимость позиции с учетом количества.</param>
public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    string ImageUrl,
    int Quantity,
    decimal Price,
    decimal TotalPrice);
