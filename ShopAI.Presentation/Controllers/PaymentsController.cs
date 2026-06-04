using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Admin")]
public class PaymentsController(AppDbContext context, IUserContext userContext) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CancellationToken ct)
    {
        var userId = userContext.UserId;
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return Unauthorized();

        var cart = await context.Set<Cart>()
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart == null || cart.Items.Count == 0)
            return BadRequest("Cart is empty.");

        var orders = new List<Order>();
        foreach (var group in cart.Items.GroupBy(i => i.Product.ShopId))
        {
            var order = new Order(group.Key, user)
            {
                UserId = userId
            };
            foreach (var item in group)
            {
                order.Items.Add(new OrderItem(item.ProductId, item.Product.Price, item.Quantity));
            }

            orders.Add(order);
            await context.Orders.AddAsync(order, ct);
        }

        context.Set<CartItem>().RemoveRange(cart.Items);
        cart.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        var amount = orders.SelectMany(o => o.Items).Sum(i => i.PriceAtPurchase * i.Quantity);
        var paymentId = Guid.NewGuid();

        return Ok(new CheckoutResponse(
            paymentId,
            orders.Select(o => o.Id).ToList(),
            amount,
            "RUB",
            "requires_confirmation",
            $"/api/Payments/{paymentId}/confirm"));
    }

    [HttpPost("{paymentId:guid}/confirm")]
    public async Task<ActionResult<PaymentStatusResponse>> Confirm(
        Guid paymentId,
        [FromBody] ConfirmPaymentRequest request,
        CancellationToken ct)
    {
        if (request.OrderIds.Count == 0)
            return BadRequest("orderIds is required.");

        var userId = userContext.UserId;
        var orders = await context.Orders
            .Where(o => request.OrderIds.Contains(o.Id) && o.UserId == userId)
            .ToListAsync(ct);

        if (orders.Count != request.OrderIds.Distinct().Count())
            return NotFound("One or more orders were not found.");

        foreach (var order in orders)
        {
            order.Status = OrderStatus.Processing;
        }

        await context.SaveChangesAsync(ct);
        return Ok(new PaymentStatusResponse(paymentId, request.OrderIds, "succeeded"));
    }
}

public record CheckoutResponse(
    Guid PaymentId,
    List<Guid> OrderIds,
    decimal Amount,
    string Currency,
    string Status,
    string ConfirmationUrl);

public record ConfirmPaymentRequest(List<Guid> OrderIds);

public record PaymentStatusResponse(Guid PaymentId, List<Guid> OrderIds, string Status);
