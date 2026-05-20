using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Создание новой категории для магазина.
    /// </summary>
    /// <remarks>
    /// Позволяет создать как корневую категорию, так и подкатегорию (если указан ParentId).
    /// Категория всегда должна быть привязана к конкретному ShopId.
    /// </remarks>
    /// <param name="command">Данные категории: имя, ID магазина и опционально ID родительской категории.</param>
    /// <returns>Идентификатор созданной категории.</returns>
    /// <response code="201">Категория успешно создана.</response>
    /// <response code="400">Ошибка валидации (например, пустое имя).</response>
    /// <response code="404">Магазин или родительская категория не найдены.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateCategoryCommand command)
    {
        var categoryId = await mediator.Send(command);
        
        return CreatedAtAction(nameof(Create), new { id = categoryId }, categoryId);
    }

    /// <summary>
    /// Получить все категории конкретного магазина.
    /// </summary>
    /// <param name="shopId">Идентификатор магазина.</param>
    /// <response code="200">Список категорий успешно получен.</response>
    [HttpGet("shop/{shopId:guid}")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryDto>>> GetByShopId(Guid shopId)
    {
        var result = await mediator.Send(new GetCategoriesByShopIdQuery(shopId));
        return Ok(result);
    }
}