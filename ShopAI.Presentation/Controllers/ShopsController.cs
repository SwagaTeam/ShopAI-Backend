using System.Net.Mime;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // По умолчанию все действия требуют авторизации
[Produces(MediaTypeNames.Application.Json)]
public class ShopsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Получить список магазинов, сгруппированных по категориям.
    /// </summary>
    /// <remarks>
    /// Используется для построения главной страницы или навигационного меню. 
    /// Логика группировки:
    /// 1. Собираются все активные категории.
    /// 2. Для каждой категории подтягиваются магазины, у которых есть товары в этой категории.
    /// </remarks>
    /// <returns>Коллекция объектов ShopsByCategoryVm.</returns>
    /// <response code="200">Успешное получение списка.</response>
    /// <response code="204">В системе еще нет ни одного магазина или категории.</response>
    [HttpGet("by-categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ShopsByCategoryVm>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<List<ShopsByCategoryVm>>> GetShopsByCategories()
    {
        var result = await mediator.Send(new GetShopsByCategoryListQuery());
        return result.Count == 0 ? NoContent() : Ok(result);
    }

    /// <summary>
    /// Создать новый магазин для текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Владельцем магазина автоматически становится пользователь, чей токен использован в запросе.
    /// UrlAlias должен быть уникальным в пределах всей системы.
    /// </remarks>
    /// <param name="command">Объект с названием, описанием, лого и коротким URL-именем магазина.</param>
    /// <returns>Идентификатор (GUID) созданного магазина.</returns>
    /// <response code="201">Магазин успешно создан.</response>
    /// <response code="400">Ошибка валидации или дубликат UrlAlias.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpPost]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateShopCommand command)
    {
        var id = await mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Частичное или полное обновление данных магазина.
    /// </summary>
    /// <remarks>
    /// Доступно только владельцу магазина. Можно изменить название и URL-псевдоним.
    /// </remarks>
    /// <param name="id">Идентификатор магазина.</param>
    /// <param name="request">Новые данные для обновления.</param>
    /// <response code="200">Данные успешно обновлены.</response>
    /// <response code="403">Попытка редактирования чужого магазина.</response>
    /// <response code="404">Магазин с таким ID не существует.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(typeof(ShopDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShopRequest request)
    {
        var shop = await mediator.Send(new UpdateShopCommand(id, request.Name, request.UrlAlias));
        return Ok(shop);
    }

    /// <summary>
    /// Безвозвратное удаление магазина и всех его настроек.
    /// </summary>
    /// <remarks>
    /// Внимание: удаление магазина может повлечь за собой удаление связанных категорий (Cascade).
    /// Проверяется право собственности пользователя на данный ресурс.
    /// </remarks>
    /// <param name="id">Идентификатор удаляемого магазина.</param>
    /// <response code="200">Магазин успешно удален.</response>
    /// <response code="403">Недостаточно прав для удаления.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteShopCommand(id));
        return Ok(id);
    }

    /// <summary>
    /// Получить подробную информацию о магазине по его ID.
    /// </summary>
    /// <remarks>
    /// Возвращает публичные данные магазина, включая информацию о владельце и визуальную тему.
    /// </remarks>
    /// <param name="id">GUID магазина.</param>
    /// <response code="200">Магазин найден и данные возвращены.</response>
    /// <response code="404">Магазин с таким идентификатором не найден.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ShopDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopDto>> GetById(Guid id)
    {
        var result = await mediator.Send(new GetShopByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Получить список магазинов текущего авторизованного пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает все магазины, владельцем которых является пользователь (определяется по JWT токену).
    /// </remarks>
    /// <returns>Список магазинов пользователя.</returns>
    /// <response code="200">Успешное получение списка.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet("my")]
    [Authorize(Roles = "User,Seller,Admin")]
    [ProducesResponseType(typeof(List<ShopDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShopDto>>> GetMyShops()
    {
        var result = await mediator.Send(new GetMyShopsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Получить список товаров конкретного магазина (с пагинацией).
    /// </summary>
    /// <param name="shopId">Идентификатор магазина (GUID).</param>
    /// <param name="page">Номер страницы (начиная с 1).</param>
    /// <param name="pageSize">Количество товаров на одной странице.</param>
    [HttpGet("{shopId:guid}/products")]
    [ProducesResponseType(typeof(PagedListDto<ProductShortDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProducts(
        Guid shopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetShopProductsQuery(shopId, page, pageSize);
            var result = await mediator.Send(query);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
