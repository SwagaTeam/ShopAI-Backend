using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
[Authorize(Roles = "User,Seller,Admin")]
public class PaymentsController(
    AppDbContext context,
    IUserContext userContext,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        [FromBody] CheckoutRequest? request,
        CancellationToken ct)
    {
        var userId = userContext.UserId;
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return Unauthorized();

        var cart = await context.Set<Cart>()
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart == null) return BadRequest("Cart is empty.");

        var items = cart.Items.Where(i => i.Quantity > 0).ToList();
        if (items.Count == 0)
        {
            context.Set<CartItem>().RemoveRange(cart.Items);
            await context.SaveChangesAsync(ct);
            return BadRequest("Cart is empty.");
        }

        var orders = new List<Order>();
        foreach (var group in items.GroupBy(i => i.Product.ShopId))
        {
            var order = new Order(group.Key, user)
            {
                UserId = userId,
                PaymentProvider = "YooKassa",
                PaymentStatus = "created"
            };

            foreach (var item in group)
            {
                order.Items.Add(new OrderItem(item.ProductId, item.Product.Price, item.Quantity));
            }

            orders.Add(order);
            await context.Orders.AddAsync(order, ct);
        }

        await context.SaveChangesAsync(ct);

        var amount = orders.SelectMany(o => o.Items).Sum(i => i.PriceAtPurchase * i.Quantity);
        var payment = await CreatePaymentAsync(orders, amount, request?.ReturnUrl, ct);

        foreach (var order in orders)
        {
            order.PaymentProviderId = payment.ProviderPaymentId;
            order.PaymentStatus = payment.Status;
            order.PaymentConfirmationUrl = payment.ConfirmationUrl;
        }

        context.Set<CartItem>().RemoveRange(cart.Items);
        cart.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new CheckoutResponse(
            payment.PaymentId,
            orders.Select(o => o.Id).ToList(),
            amount,
            "RUB",
            payment.Status,
            payment.ConfirmationUrl));
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
            order.PaymentStatus = "succeeded";
            order.PaidAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
        return Ok(new PaymentStatusResponse(paymentId, request.OrderIds, "succeeded"));
    }

    [AllowAnonymous]
    [HttpPost("yookassa/webhook")]
    public async Task<IActionResult> YooKassaWebhook([FromBody] JsonElement payload, CancellationToken ct)
    {
        var paymentObject = payload.TryGetProperty("object", out var obj) ? obj : payload;
        var eventName = payload.TryGetProperty("event", out var eventElement)
            ? eventElement.GetString()
            : paymentObject.GetPropertyOrDefault("status");

        var providerPaymentId = paymentObject.GetPropertyOrDefault("id");
        if (string.IsNullOrWhiteSpace(providerPaymentId)) return BadRequest("payment id is required.");

        var orders = await context.Orders
            .Where(o => o.PaymentProviderId == providerPaymentId)
            .ToListAsync(ct);

        if (orders.Count == 0) return NotFound();

        var status = paymentObject.GetPropertyOrDefault("status") ?? eventName ?? "unknown";
        var succeeded = string.Equals(eventName, "payment.succeeded", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase);
        var canceled = string.Equals(eventName, "payment.canceled", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase);

        foreach (var order in orders)
        {
            order.PaymentStatus = status;
            if (succeeded)
            {
                order.Status = OrderStatus.Processing;
                order.PaidAtUtc = DateTime.UtcNow;
            }
            else if (canceled)
            {
                order.Status = OrderStatus.Cancelled;
            }
        }

        await context.SaveChangesAsync(ct);
        return Ok();
    }

    private async Task<CreatedPayment> CreatePaymentAsync(
        IReadOnlyCollection<Order> orders,
        decimal amount,
        string? returnUrl,
        CancellationToken ct)
    {
        var shopId = configuration["YooKassa:ShopId"];
        var secretKey = configuration["YooKassa:SecretKey"];
        var paymentId = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(shopId) || string.IsNullOrWhiteSpace(secretKey))
        {
            return new CreatedPayment(
                paymentId,
                paymentId.ToString(),
                "requires_confirmation",
                $"/api/Payments/{paymentId}/confirm");
        }

        var client = httpClientFactory.CreateClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shopId}:{secretKey}"));
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.yookassa.ru/v3/payments");
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        message.Headers.Add("Idempotence-Key", paymentId.ToString());

        var publicReturnUrl = returnUrl
                              ?? configuration["YooKassa:ReturnUrl"]
                              ?? $"{Request.Scheme}://{Request.Host}/checkout";
        var orderIds = string.Join(",", orders.Select(o => o.Id));
        message.Content = JsonContent.Create(new
        {
            amount = new
            {
                value = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                currency = "RUB"
            },
            capture = true,
            confirmation = new
            {
                type = "redirect",
                return_url = publicReturnUrl
            },
            description = $"ShopAI order {orders.First().Id}",
            metadata = new
            {
                order_ids = orderIds
            }
        });

        using var response = await client.SendAsync(message, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"YooKassa payment creation failed: {content}");

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var providerPaymentId = root.GetProperty("id").GetString() ?? paymentId.ToString();
        var status = root.GetPropertyOrDefault("status") ?? "pending";
        var confirmationUrl = root.TryGetProperty("confirmation", out var confirmation)
            ? confirmation.GetPropertyOrDefault("confirmation_url") ?? string.Empty
            : string.Empty;

        return new CreatedPayment(paymentId, providerPaymentId, status, confirmationUrl);
    }
}

public record CheckoutRequest(string? ReturnUrl);

public record CheckoutResponse(
    Guid PaymentId,
    List<Guid> OrderIds,
    decimal Amount,
    string Currency,
    string Status,
    string ConfirmationUrl);

public record ConfirmPaymentRequest(List<Guid> OrderIds);

public record PaymentStatusResponse(Guid PaymentId, List<Guid> OrderIds, string Status);

internal record CreatedPayment(Guid PaymentId, string ProviderPaymentId, string Status, string ConfirmationUrl);

internal static class JsonElementExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out var property) &&
               property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }
}
