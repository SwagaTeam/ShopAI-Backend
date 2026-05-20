using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<BrandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BrandDto>>> GetAll()
    {
        var result = await mediator.Send(new GetAllBrandsQuery());
        return Ok(result);
    }

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