using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Admin")]
[Produces(MediaTypeNames.Application.Json)]
public class CartController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Получить состав корзины текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает список всех товаров, их количество и итоговую сумму. 
    /// Если корзина пуста, вернется объект с пустым списком.
    /// </remarks>
    /// <response code="200">Корзина успешно получена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CartVm), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartVm>> GetCart()
    {
        var result = await mediator.Send(new GetCartQuery());
        return Ok(result);
    }

    /// <summary>
    /// Добавить товар в корзину или увеличить его количество.
    /// </summary>
    /// <remarks>
    /// Если товар уже есть в корзине, его количество будет увеличено на указанное значение.
    /// Если корзины еще не существует, она будет создана автоматически.
    /// </remarks>
    /// <param name="command">Данные для добавления: ID товара и количество.</param>
    /// <response code="200">Товар успешно добавлен, возвращен ID корзины.</response>
    /// <response code="404">Товар с указанным ID не найден в каталоге.</response>
    [HttpPost("items")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem([FromBody] AddToCartCommand command)
    {
        var cartId = await mediator.Send(command);
        return Ok(cartId);
    }

    /// <summary>
    /// Полностью удалить товар из корзины.
    /// </summary>
    /// <remarks>
    /// Удаляет всю позицию товара из корзины текущего пользователя независимо от его количества.
    /// </remarks>
    /// <param name="productId">Идентификатор товара (Guid).</param>
    /// <response code="204">Товар успешно удален из корзины.</response>
    /// <response code="404">Корзина пользователя не найдена или товар в ней отсутствует.</response>
    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid productId)
    {
        await mediator.Send(new RemoveFromCartCommand(productId));
        return NoContent();
    }
}
