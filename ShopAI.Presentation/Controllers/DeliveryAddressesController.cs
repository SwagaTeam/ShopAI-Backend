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
    [HttpGet]
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

    [HttpPost]
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

    [HttpDelete("{id:guid}")]
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

public record CreateDeliveryAddressRequest(
    string Title,
    string AddressLine,
    string? Entrance,
    string? Floor,
    string? Apartment,
    string? Comment);

public record DeliveryAddressDto(
    Guid Id,
    string Title,
    string AddressLine,
    string? Entrance,
    string? Floor,
    string? Apartment,
    string? Comment,
    DateTime CreatedAtUtc);
