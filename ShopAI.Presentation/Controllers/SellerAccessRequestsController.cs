using System.Text.RegularExpressions;
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
public class SellerAccessRequestsController(AppDbContext context, IUserContext userContext) : ControllerBase
{
    private static readonly Regex InnRegex = new(@"^\d{10,12}$", RegexOptions.Compiled);

    [HttpPost]
    public async Task<ActionResult<SellerAccessRequestDto>> Create(
        [FromBody] CreateSellerAccessRequest request,
        CancellationToken ct)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null) return BadRequest(validationError);

        var userId = userContext.UserId;
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return Unauthorized();
        if (user.Role is Domain.Entities.User.SellerRole or Domain.Entities.User.AdminRole)
            return BadRequest("Seller access is already granted.");

        var hasPending = await context.SellerAccessRequests
            .AnyAsync(r => r.UserId == userId && r.Status == SellerAccessRequestStatus.Pending, ct);
        if (hasPending) return BadRequest("You already have a pending seller access request.");

        var entity = new SellerAccessRequest
        {
            UserId = userId,
            InnOrOgrnip = request.InnOrOgrnip.Trim(),
            SocialOrWebsiteUrl = request.SocialOrWebsiteUrl.Trim(),
            PlannedCategory = ParseCategory(request.PlannedCategory),
            Description = request.Description.Trim(),
            AcceptedMarketplaceRules = request.AcceptedMarketplaceRules,
            Status = SellerAccessRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        await context.SellerAccessRequests.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetMyRequests), new { id = entity.Id }, ToDto(entity, user));
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<SellerAccessRequestDto>>> GetMyRequests(CancellationToken ct)
    {
        var userId = userContext.UserId;
        var requests = await context.SellerAccessRequests
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(requests.Select(r => ToDto(r, r.User)).ToList());
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<SellerAccessRequestDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = context.SellerAccessRequests
            .AsNoTracking()
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsedStatus = ParseStatus(status);
            query = query.Where(r => r.Status == parsedStatus);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(requests.Select(r => ToDto(r, r.User)).ToList());
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SellerAccessRequestDto>> Approve(
        Guid id,
        [FromBody] ReviewSellerAccessRequest? request,
        CancellationToken ct)
    {
        var entity = await context.SellerAccessRequests
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound();
        if (entity.Status != SellerAccessRequestStatus.Pending)
            return BadRequest("Only pending requests can be approved.");
        if (entity.User == null) return BadRequest("Request user was not found.");

        entity.Status = SellerAccessRequestStatus.Approved;
        entity.ReviewedAtUtc = DateTime.UtcNow;
        entity.ReviewedByAdminId = userContext.UserId;
        entity.AdminComment = request?.AdminComment;
        entity.User.Role = Domain.Entities.User.SellerRole;

        await context.SaveChangesAsync(ct);
        return Ok(ToDto(entity, entity.User));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SellerAccessRequestDto>> Reject(
        Guid id,
        [FromBody] ReviewSellerAccessRequest? request,
        CancellationToken ct)
    {
        var entity = await context.SellerAccessRequests
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound();
        if (entity.Status != SellerAccessRequestStatus.Pending)
            return BadRequest("Only pending requests can be rejected.");

        entity.Status = SellerAccessRequestStatus.Rejected;
        entity.ReviewedAtUtc = DateTime.UtcNow;
        entity.ReviewedByAdminId = userContext.UserId;
        entity.AdminComment = request?.AdminComment;

        await context.SaveChangesAsync(ct);
        return Ok(ToDto(entity, entity.User));
    }

    private static string? ValidateRequest(CreateSellerAccessRequest request)
    {
        if (!InnRegex.IsMatch(request.InnOrOgrnip.Trim()))
            return "INN/OGRNIP must contain 10-12 digits.";
        if (!Uri.TryCreate(request.SocialOrWebsiteUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return "Social network or website URL must be a valid absolute URL.";
        if (!Enum.TryParse<PlannedProductCategory>(request.PlannedCategory, true, out _))
            return "plannedCategory must be one of: electronics, clothing, adultGoods, other.";
        if (request.Description.Trim().Length is < 1 or > 300)
            return "Description must be 1-300 characters.";
        if (!request.AcceptedMarketplaceRules)
            return "Marketplace rules must be accepted.";
        return null;
    }

    private static PlannedProductCategory ParseCategory(string value)
    {
        var normalized = value.Trim();
        if (normalized.Equals("adultGoods", StringComparison.OrdinalIgnoreCase))
            return PlannedProductCategory.AdultGoods;

        return Enum.Parse<PlannedProductCategory>(normalized, true);
    }

    private static SellerAccessRequestStatus ParseStatus(string value)
        => Enum.Parse<SellerAccessRequestStatus>(value.Trim(), true);

    private static SellerAccessRequestDto ToDto(SellerAccessRequest request, User? user)
        => new(
            request.Id,
            request.UserId,
            user?.FullName,
            user?.Email,
            request.InnOrOgrnip,
            request.SocialOrWebsiteUrl,
            ToCategoryString(request.PlannedCategory),
            request.Description,
            request.AcceptedMarketplaceRules,
            request.Status.ToString(),
            request.CreatedAtUtc,
            request.ReviewedAtUtc,
            request.AdminComment);

    private static string ToCategoryString(PlannedProductCategory category)
        => category == PlannedProductCategory.AdultGoods
            ? "adultGoods"
            : char.ToLowerInvariant(category.ToString()[0]) + category.ToString()[1..];
}

public record CreateSellerAccessRequest(
    string InnOrOgrnip,
    string SocialOrWebsiteUrl,
    string PlannedCategory,
    string Description,
    bool AcceptedMarketplaceRules);

public record ReviewSellerAccessRequest(string? AdminComment);

public record SellerAccessRequestDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    string? UserEmail,
    string InnOrOgrnip,
    string SocialOrWebsiteUrl,
    string PlannedCategory,
    string Description,
    bool AcceptedMarketplaceRules,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    string? AdminComment);
