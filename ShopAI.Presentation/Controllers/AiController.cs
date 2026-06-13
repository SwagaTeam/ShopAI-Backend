using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Универсальный AI-поиск товаров по свободному текстовому запросу.
    /// </summary>
    /// <remarks>
    /// CategoryId необязателен: если не передан, поиск выполняется по всему каталогу.
    /// Если передан, поиск будет ограничен указанной категорией.
    /// Чтобы добавить предложенный AI-бандл в корзину или избранное, возьмите productIds из нужного элемента bundles и отправьте их в
    /// POST /api/Cart/bundles или POST /api/Favorites/bundles.
    /// </remarks>
    /// <param name="request">
    /// userPrompt - текст запроса пользователя (обязательно).
    /// budgetMin/budgetMax - опциональные границы бюджета.
    /// categoryId - опциональное сужение поиска.
    /// limit - опциональный лимит количества товаров.
    /// </param>
    /// <response code="200">Успешный ответ с интерпретацией запроса и подборкой товаров.</response>
    /// <response code="400">Пустой или некорректный запрос.</response>
    [HttpPost("shopping-assistant")]
    [ProducesResponseType(typeof(ShoppingAssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShoppingAssistantResponse>> ShoppingAssistant([FromBody] ShoppingAssistantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserPrompt))
            return BadRequest("userPrompt is required");

        var result = await mediator.Send(new ShoppingAssistantQuery(request));
        return Ok(result);
    }

    /// <summary>
    /// Сгенерировать теги для товара на основе его названия, описания и характеристик.
    /// </summary>
    /// <remarks>
    /// Используется для автозаполнения тегов в карточке товара. Чем подробнее переданы description и attributes, тем точнее будет результат.
    /// </remarks>
    /// <param name="request">Данные товара, по которым нужно подобрать теги.</param>
    /// <returns>Список сгенерированных тегов.</returns>
    /// <response code="200">Теги успешно сгенерированы.</response>
    /// <response code="400">Не передано обязательное название товара.</response>
    [HttpPost("product-tags")]
    [ProducesResponseType(typeof(GenerateProductTagsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerateProductTagsResponse>> GenerateProductTags(
        [FromBody] GenerateProductTagsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("name is required");

        var result = await mediator.Send(new GenerateProductTagsQuery(request));
        return Ok(result);
    }
}
