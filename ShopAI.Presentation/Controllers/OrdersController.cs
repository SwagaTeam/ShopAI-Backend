using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Seller,Admin")]
public class OrdersController(AppDbContext context, IUserContext userContext) : ControllerBase
{
    [HttpGet("my")]
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

        return Ok(orders.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
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

        return Ok(ToDto(order));
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

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    string ImageUrl,
    int Quantity,
    decimal Price,
    decimal TotalPrice);
