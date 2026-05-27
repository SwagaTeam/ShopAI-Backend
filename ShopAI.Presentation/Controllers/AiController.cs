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
    public async Task<ActionResult<ShoppingAssistantResponse>> ShoppingAssistant([FromBody] ShoppingAssistantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserPrompt))
            return BadRequest("userPrompt is required");

        var result = await mediator.Send(new ShoppingAssistantQuery(request));
        return Ok(result);
    }
}
