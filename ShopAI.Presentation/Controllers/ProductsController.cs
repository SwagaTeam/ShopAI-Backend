using System.ComponentModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")] 
public class ProductsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Получение данных для главной страницы.
    /// </summary>
    /// <remarks>
    /// Возвращает два списка: самые новые товары и самые популярные (на основе рейтинга/продаж).
    /// </remarks>
    /// <param name="count">Количество товаров в каждом списке (минимум 1, максимум 50). По умолчанию: 10.</param>
    /// <returns>Объект с двумя списками товаров.</returns>
    /// <response code="200">Данные успешно получены.</response>
    /// <response code="400">Некорректный параметр count.</response>
    [HttpGet("main-page")]
    [ProducesResponseType(typeof(MainPageProductsVm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MainPageProductsVm>> GetMainPage(
        [FromQuery, DefaultValue(10)] int count = 10)
    {
        if (count <= 0) return BadRequest("Count must be greater than 0");
        
        var query = new GetMainPageProductsQuery(count);
        var result = await mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Создание нового товара и привязка его к магазину.
    /// </summary>
    /// <param name="command">Данные товара (название, цена, категория, магазин и т.д.)</param>
    /// <returns>Идентификатор созданного товара.</returns>
    /// <response code="201">Товар успешно создан.</response>
    /// <response code="400">Ошибка валидации входных данных.</response>
    /// <response code="404">Указанный магазин или категория не найдены.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var productId = await mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = productId }, productId);
    }

    /// <summary>
    /// Получение детальной информации о конкретном товаре по его ID.
    /// </summary>
    /// <param name="id">Идентификатор товара (GUID).</param>
    /// <response code="200">Данные товара успешно получены.</response>
    /// <response code="404">Товар с указанным ID не найден.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetById(Guid id)
    {
        try
        {
            var result = await mediator.Send(new GetProductByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
