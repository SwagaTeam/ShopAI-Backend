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
    /// <summary>
    /// Создать платеж по товарам из корзины текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Корзина разбивается на заказы по магазинам, после чего создается платеж YooKassa или локальный fallback-платеж, если настройки YooKassa не заданы.
    /// После успешного создания платежа корзина очищается.
    /// </remarks>
    /// <param name="request">Данные оформления: адрес доставки, телефон, комментарий и URL возврата после оплаты.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Данные созданного платежа, список заказов и ссылка для подтверждения оплаты.</returns>
    /// <response code="200">Платеж и связанные заказы успешно созданы.</response>
    /// <response code="400">Некорректные данные оформления или пустая корзина.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        [FromBody] CheckoutRequest? request,
        CancellationToken ct)
    {
        var userId = userContext.UserId;
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return Unauthorized();
        if (request == null) return BadRequest("Checkout data is required.");

        var delivery = await ResolveDeliveryAsync(userId, request, ct);
        if (!delivery.IsValid)
            return BadRequest(delivery.Error);

        var contactPhone = request.ContactPhone?.Trim() ?? string.Empty;
        if (contactPhone.Length is < 5 or > 30)
            return BadRequest("Contact phone must contain 5-30 characters.");

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment?.Length > 500)
            return BadRequest("Comment must be 500 characters or less.");

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
                DeliveryAddressId = delivery.AddressId,
                DeliveryAddress = delivery.AddressText!,
                ContactPhone = contactPhone,
                Comment = comment,
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

    /// <summary>
    /// Подтвердить тестовый или fallback-платеж вручную.
    /// </summary>
    /// <remarks>
    /// Используется, когда платежный провайдер не настроен и API вернул confirmationUrl вида /api/Payments/{paymentId}/confirm.
    /// Переводит указанные заказы текущего пользователя в статус обработки и помечает оплату как succeeded.
    /// </remarks>
    /// <param name="paymentId">Локальный идентификатор платежа.</param>
    /// <param name="request">Список заказов, которые нужно подтвердить как оплаченные.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Статус подтвержденного платежа.</returns>
    /// <response code="200">Платеж успешно подтвержден.</response>
    /// <response code="400">Не передан список заказов.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Один или несколько заказов не найдены.</response>
    [HttpPost("{paymentId:guid}/confirm")]
    [ProducesResponseType(typeof(PaymentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Принять webhook от YooKassa об изменении статуса платежа.
    /// </summary>
    /// <param name="payload">JSON-событие YooKassa. Идентификатор платежа берется из поля object.id или id.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="200">Webhook обработан, статусы связанных заказов обновлены.</response>
    /// <response code="400">В payload отсутствует идентификатор платежа.</response>
    /// <response code="404">Заказы с таким платежом не найдены.</response>
    [AllowAnonymous]
    [HttpPost("yookassa/webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    private async Task<ResolvedDelivery> ResolveDeliveryAsync(Guid userId, CheckoutRequest request, CancellationToken ct)
    {
        if (request.DeliveryAddressId.HasValue)
        {
            var address = await context.DeliveryAddresses
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == request.DeliveryAddressId.Value && a.UserId == userId, ct);

            if (address == null)
                return ResolvedDelivery.Invalid("Delivery address was not found.");

            return ResolvedDelivery.Valid(address.Id, address.AddressLine);
        }

        var addressText = request.DeliveryAddressText?.Trim() ?? string.Empty;
        if (addressText.Length is < 10 or > 500)
            return ResolvedDelivery.Invalid("Delivery address must contain 10-500 characters.");

        return ResolvedDelivery.Valid(null, addressText);
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

/// <summary>
/// Данные оформления заказа и создания платежа.
/// </summary>
/// <param name="ReturnUrl">URL, на который YooKassa вернет пользователя после оплаты. Если не передан, используется настройка YooKassa:ReturnUrl.</param>
/// <param name="DeliveryAddressId">Идентификатор сохраненного адреса доставки текущего пользователя.</param>
/// <param name="DeliveryAddressText">Текст нового адреса доставки, если DeliveryAddressId не используется. Длина от 10 до 500 символов.</param>
/// <param name="ContactPhone">Контактный телефон получателя. Длина от 5 до 30 символов.</param>
/// <param name="Comment">Комментарий к заказу. Максимум 500 символов.</param>
public record CheckoutRequest(
    string? ReturnUrl,
    Guid? DeliveryAddressId,
    string? DeliveryAddressText,
    string? ContactPhone,
    string? Comment);

/// <summary>
/// Ответ с данными созданного платежа.
/// </summary>
/// <param name="PaymentId">Локальный идентификатор платежа.</param>
/// <param name="OrderIds">Идентификаторы заказов, созданных из корзины.</param>
/// <param name="Amount">Итоговая сумма платежа.</param>
/// <param name="Currency">Валюта платежа.</param>
/// <param name="Status">Статус платежа у провайдера или fallback-статус.</param>
/// <param name="ConfirmationUrl">Ссылка для перехода к оплате или локального подтверждения.</param>
public record CheckoutResponse(
    Guid PaymentId,
    List<Guid> OrderIds,
    decimal Amount,
    string Currency,
    string Status,
    string ConfirmationUrl);

/// <summary>
/// Запрос на ручное подтверждение платежа.
/// </summary>
/// <param name="OrderIds">Идентификаторы заказов текущего пользователя, которые нужно пометить оплаченными.</param>
public record ConfirmPaymentRequest(List<Guid> OrderIds);

/// <summary>
/// Ответ со статусом платежа после подтверждения.
/// </summary>
/// <param name="PaymentId">Идентификатор платежа.</param>
/// <param name="OrderIds">Идентификаторы подтвержденных заказов.</param>
/// <param name="Status">Итоговый статус платежа.</param>
public record PaymentStatusResponse(Guid PaymentId, List<Guid> OrderIds, string Status);

internal record CreatedPayment(Guid PaymentId, string ProviderPaymentId, string Status, string ConfirmationUrl);

internal record ResolvedDelivery(bool IsValid, Guid? AddressId, string? AddressText, string? Error)
{
    public static ResolvedDelivery Valid(Guid? addressId, string addressText) => new(true, addressId, addressText, null);
    public static ResolvedDelivery Invalid(string error) => new(false, null, null, error);
}

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
