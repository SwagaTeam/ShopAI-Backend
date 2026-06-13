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

    /// <summary>
    /// Создать заявку на получение доступа продавца.
    /// </summary>
    /// <remarks>
    /// Доступно обычному авторизованному пользователю. У пользователя может быть только одна заявка в статусе Pending.
    /// После создания заявка ожидает решения администратора.
    /// </remarks>
    /// <param name="request">Данные заявки: ИНН/ОГРНИП, ссылка на сайт или соцсеть, планируемая категория, описание и принятие правил.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Созданная заявка на доступ продавца.</returns>
    /// <response code="201">Заявка успешно создана.</response>
    /// <response code="400">Некорректные данные заявки или доступ продавца уже есть.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SellerAccessRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Получить свои заявки на доступ продавца.
    /// </summary>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Список заявок текущего пользователя от новых к старым.</returns>
    /// <response code="200">Список заявок успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<SellerAccessRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Получить все заявки на доступ продавца для администрирования.
    /// </summary>
    /// <param name="status">Необязательный фильтр по статусу заявки: Pending, Approved или Rejected.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Список заявок, отсортированный от новых к старым.</returns>
    /// <response code="200">Список заявок успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Пользователь не является администратором.</response>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<SellerAccessRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>
    /// Одобрить заявку на доступ продавца.
    /// </summary>
    /// <remarks>
    /// Переводит заявку в статус Approved и меняет роль пользователя на Seller.
    /// Одобрить можно только заявку в статусе Pending.
    /// </remarks>
    /// <param name="id">Идентификатор заявки.</param>
    /// <param name="request">Необязательный комментарий администратора к решению.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Обновленная заявка.</returns>
    /// <response code="200">Заявка одобрена.</response>
    /// <response code="400">Заявка уже рассмотрена или связанный пользователь не найден.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Пользователь не является администратором.</response>
    /// <response code="404">Заявка не найдена.</response>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SellerAccessRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Отклонить заявку на доступ продавца.
    /// </summary>
    /// <remarks>
    /// Переводит заявку в статус Rejected. Отклонить можно только заявку в статусе Pending.
    /// </remarks>
    /// <param name="id">Идентификатор заявки.</param>
    /// <param name="request">Необязательный комментарий администратора с причиной отклонения.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Обновленная заявка.</returns>
    /// <response code="200">Заявка отклонена.</response>
    /// <response code="400">Заявка уже рассмотрена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Пользователь не является администратором.</response>
    /// <response code="404">Заявка не найдена.</response>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SellerAccessRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

/// <summary>
/// Запрос на получение роли продавца.
/// </summary>
/// <param name="InnOrOgrnip">ИНН или ОГРНИП заявителя. Допускается 10-12 цифр.</param>
/// <param name="SocialOrWebsiteUrl">Абсолютная ссылка на сайт или профиль в социальной сети заявителя.</param>
/// <param name="PlannedCategory">Планируемая категория товаров: electronics, clothing, adultGoods или other.</param>
/// <param name="Description">Короткое описание продавца или ассортимента. Длина от 1 до 300 символов.</param>
/// <param name="AcceptedMarketplaceRules">Подтверждение принятия правил маркетплейса. Должно быть true.</param>
public record CreateSellerAccessRequest(
    string InnOrOgrnip,
    string SocialOrWebsiteUrl,
    string PlannedCategory,
    string Description,
    bool AcceptedMarketplaceRules);

/// <summary>
/// Комментарий администратора при рассмотрении заявки.
/// </summary>
/// <param name="AdminComment">Необязательный комментарий к одобрению или отклонению заявки.</param>
public record ReviewSellerAccessRequest(string? AdminComment);

/// <summary>
/// Заявка на получение доступа продавца.
/// </summary>
/// <param name="Id">Идентификатор заявки.</param>
/// <param name="UserId">Идентификатор пользователя, отправившего заявку.</param>
/// <param name="UserName">Имя пользователя.</param>
/// <param name="UserEmail">Email пользователя.</param>
/// <param name="InnOrOgrnip">ИНН или ОГРНИП заявителя.</param>
/// <param name="SocialOrWebsiteUrl">Ссылка на сайт или соцсеть заявителя.</param>
/// <param name="PlannedCategory">Планируемая категория товаров.</param>
/// <param name="Description">Описание продавца или ассортимента.</param>
/// <param name="AcceptedMarketplaceRules">Признак принятия правил маркетплейса.</param>
/// <param name="Status">Текущий статус заявки.</param>
/// <param name="CreatedAtUtc">Дата и время создания заявки в UTC.</param>
/// <param name="ReviewedAtUtc">Дата и время рассмотрения заявки в UTC.</param>
/// <param name="AdminComment">Комментарий администратора.</param>
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
