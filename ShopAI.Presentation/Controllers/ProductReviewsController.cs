using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/reviews")]
[Produces(MediaTypeNames.Application.Json)]
public class ProductReviewsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Получить список отзывов к товару.
    /// </summary>
    /// <param name="productId">Идентификатор товара.</param>
    /// <param name="page">Номер страницы (начиная с 1).</param>
    /// <param name="pageSize">Количество отзывов на страницу.</param>
    /// <response code="200">Список отзывов успешно получен.</response>
    [HttpGet]
    [AllowAnonymous] // Отзывы могут читать даже неавторизованные гости
    [ProducesResponseType(typeof(List<ProductReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductReviewDto>>> GetReviews(
        Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await mediator.Send(new GetProductReviewsQuery(productId, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Оставить отзыв к товару.
    /// </summary>
    /// <remarks>
    /// Доступно только авторизованным пользователям. Оценка должна быть от 1 до 5. 
    /// Один пользователь может оставить только один отзыв на один и тот же товар.
    /// </remarks>
    /// <param name="productId">Идентификатор товара.</param>
    /// <param name="request">Данные отзыва (оценка и комментарий).</param>
    /// <response code="200">Отзыв успешно добавлен. Возвращает ID созданного отзыва.</response>
    /// <response code="400">Неверная оценка или попытка оставить дубликат отзыва.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> AddReview(Guid productId, [FromBody] AddReviewRequest request)
    {
        // Перекладываем данные в команду медиатора, добавляя productId из URL
        var command = new AddProductReviewCommand(productId, request.Rating, request.Comment);
        var reviewId = await mediator.Send(command);
        return Ok(reviewId);
    }
}

/// <summary>
/// Модель запроса для добавления отзыва.
/// </summary>
/// <param name="Rating">Оценка товара от 1 до 5.</param>
/// <param name="Comment">Текстовый комментарий к отзыву.</param>
public record AddReviewRequest(int Rating, string Comment);
