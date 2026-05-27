using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Получить список всех брендов.
    /// </summary>
    /// <response code="200">Список брендов успешно получен.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<BrandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BrandDto>>> GetAll()
    {
        var result = await mediator.Send(new GetAllBrandsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Получить бренд по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор бренда (GUID).</param>
    /// <response code="200">Бренд найден.</response>
    /// <response code="404">Бренд не найден.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BrandDto>> GetById(Guid id)
    {
        try
        {
            var result = await mediator.Send(new GetBrandByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Бренд не найден" });
        }
    }

    /// <summary>
    /// Создать новый бренд.
    /// </summary>
    /// <param name="command">Данные бренда: название и ссылка на логотип.</param>
    /// <response code="201">Бренд успешно создан.</response>
    /// <response code="400">Некорректные данные запроса.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateBrandCommand command)
    {
        try
        {
            var brandId = await mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = brandId }, brandId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Обновить существующий бренд.
    /// </summary>
    /// <param name="id">Идентификатор бренда (GUID).</param>
    /// <param name="request">Новые данные бренда.</param>
    /// <response code="204">Бренд успешно обновлен.</response>
    /// <response code="400">Некорректные данные запроса.</response>
    /// <response code="404">Бренд не найден.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBrandRequest request)
    {
        try
        {
            // Формируем команду, объединяя ID из роута и данные из тела запроса
            var command = new UpdateBrandCommand(id, request.Name, request.LogoUrl);
            await mediator.Send(command);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Бренд не найден" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Удалить бренд.
    /// </summary>
    /// <param name="id">Идентификатор бренда (GUID).</param>
    /// <response code="204">Бренд успешно удален.</response>
    /// <response code="404">Бренд не найден.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await mediator.Send(new DeleteBrandCommand(id));
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Бренд не найден" });
        }
    }
}

// Реквест для PUT, чтобы не заставлять фронтенд дублировать ID в теле запроса
public record UpdateBrandRequest(string Name, string LogoUrl);
