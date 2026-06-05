using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "User,Seller,Admin")]
    [Produces(MediaTypeNames.Application.Json)]
    public class RecentlyViewedController(IMediator mediator) : ControllerBase
    {
        /// <summary>
        /// Получить историю просмотренных товаров.
        /// </summary>
        /// <remarks>
        /// Возвращает список товаров, отсортированный от последних просмотренных к старым.
        /// </remarks>
        /// <param name="limit">Количество товаров для возврата (по умолчанию 10, максимум 20).</param>
        /// <response code="200">История успешно получена.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<ProductShortDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ProductShortDto>>> GetHistory([FromQuery] int limit = 10)
        {
            // Защита от слишком больших запросов
            if (limit > 20)
                limit = 20;

            var result = await mediator.Send(new GetRecentlyViewedQuery(limit));
            return Ok(result);
        }

        /// <summary>
        /// Зафиксировать просмотр товара.
        /// </summary>
        /// <remarks>
        /// Этот метод нужно вызывать (в фоне) каждый раз, когда пользователь открывает карточку товара.
        /// </remarks>
        /// <param name="productId">Идентификатор просмотренного товара.</param>
        /// <response code="204">Просмотр успешно зафиксирован.</response>
        /// <response code="404">Товар не найден.</response>
        [HttpPost("{productId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TrackView(Guid productId)
        {
            await mediator.Send(new TrackProductViewCommand(productId));
            return NoContent();
        }
    }
}
