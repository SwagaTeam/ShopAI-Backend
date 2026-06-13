using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Seller,Admin")]
public class DeliveryAddressesController(AppDbContext context, IUserContext userContext) : ControllerBase
{
    /// <summary>
    /// Получить адреса доставки текущего пользователя.
    /// </summary>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Список сохраненных адресов доставки от новых к старым.</returns>
    /// <response code="200">Адреса доставки успешно получены.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeliveryAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<DeliveryAddressDto>>> GetMy(CancellationToken ct)
    {
        var userId = userContext.UserId;
        var addresses = await context.DeliveryAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => ToDto(a))
            .ToListAsync(ct);

        return Ok(addresses);
    }

    /// <summary>
    /// Создать новый адрес доставки для текущего пользователя.
    /// </summary>
    /// <param name="request">Данные адреса: название, строка адреса и необязательные детали подъезда, этажа, квартиры и комментария.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Созданный адрес доставки.</returns>
    /// <response code="201">Адрес доставки успешно создан.</response>
    /// <response code="400">Некорректные данные адреса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DeliveryAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeliveryAddressDto>> Create(
        [FromBody] CreateDeliveryAddressRequest request,
        CancellationToken ct)
    {
        var validationError = Validate(request);
        if (validationError != null) return BadRequest(validationError);

        var entity = new DeliveryAddress
        {
            UserId = userContext.UserId,
            Title = request.Title.Trim(),
            AddressLine = request.AddressLine.Trim(),
            Entrance = NormalizeOptional(request.Entrance),
            Floor = NormalizeOptional(request.Floor),
            Apartment = NormalizeOptional(request.Apartment),
            Comment = NormalizeOptional(request.Comment),
            CreatedAtUtc = DateTime.UtcNow
        };

        await context.DeliveryAddresses.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetMy), new { id = entity.Id }, ToDto(entity));
    }

    /// <summary>
    /// Удалить адрес доставки текущего пользователя.
    /// </summary>
    /// <param name="id">Идентификатор адреса доставки.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="204">Адрес доставки удален.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Адрес не найден или принадлежит другому пользователю.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = userContext.UserId;
        var address = await context.DeliveryAddresses
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (address == null) return NotFound();

        context.DeliveryAddresses.Remove(address);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? Validate(CreateDeliveryAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 100)
            return "Title is required and must be 100 characters or less.";

        var address = request.AddressLine?.Trim() ?? string.Empty;
        if (address.Length is < 10 or > 500)
            return "Address line must contain 10-500 characters.";

        if (request.Comment?.Length > 300)
            return "Comment must be 300 characters or less.";

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DeliveryAddressDto ToDto(DeliveryAddress address)
    {
        return new DeliveryAddressDto(
            address.Id,
            address.Title,
            address.AddressLine,
            address.Entrance,
            address.Floor,
            address.Apartment,
            address.Comment,
            address.CreatedAtUtc);
    }
}

/// <summary>
/// Запрос на создание адреса доставки.
/// </summary>
/// <param name="Title">Короткое название адреса, например "Дом" или "Офис". Максимум 100 символов.</param>
/// <param name="AddressLine">Полная строка адреса доставки. Длина от 10 до 500 символов.</param>
/// <param name="Entrance">Подъезд или вход, если нужно уточнить доставку.</param>
/// <param name="Floor">Этаж доставки.</param>
/// <param name="Apartment">Квартира, офис или помещение.</param>
/// <param name="Comment">Комментарий курьеру. Максимум 300 символов.</param>
public record CreateDeliveryAddressRequest(
    string Title,
    string AddressLine,
    string? Entrance,
    string? Floor,
    string? Apartment,
    string? Comment);

/// <summary>
/// Адрес доставки пользователя.
/// </summary>
/// <param name="Id">Идентификатор адреса доставки.</param>
/// <param name="Title">Короткое название адреса.</param>
/// <param name="AddressLine">Полная строка адреса доставки.</param>
/// <param name="Entrance">Подъезд или вход.</param>
/// <param name="Floor">Этаж доставки.</param>
/// <param name="Apartment">Квартира, офис или помещение.</param>
/// <param name="Comment">Комментарий курьеру.</param>
/// <param name="CreatedAtUtc">Дата и время создания адреса в UTC.</param>
public record DeliveryAddressDto(
    Guid Id,
    string Title,
    string AddressLine,
    string? Entrance,
    string? Floor,
    string? Apartment,
    string? Comment,
    DateTime CreatedAtUtc);
