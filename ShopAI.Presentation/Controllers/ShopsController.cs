using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class ShopsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Получить список магазинов, сгруппированных по категориям.
    /// </summary>
    /// <remarks>
    /// Используется для построения навигации или дашборда. 
    /// Магазины группируются по уникальным именам категорий. 
    /// Один и тот же магазин может появиться в разных категориях, если у него есть соответствующие товары.
    /// </remarks>
    /// <returns>Список категорий с вложенными списками магазинов.</returns>
    /// <response code="200">Список успешно сформирован.</response>
    /// <response code="204">Магазины или категории не найдены.</response>
    [HttpGet("by-categories")]
    [ProducesResponseType(typeof(List<ShopsByCategoryVm>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<List<ShopsByCategoryVm>>> GetShopsByCategories()
    {
        var result = await mediator.Send(new GetShopsByCategoryListQuery());

        if (result.Count == 0)
        {
            return NoContent();
        }

        return Ok(result);
    }
}