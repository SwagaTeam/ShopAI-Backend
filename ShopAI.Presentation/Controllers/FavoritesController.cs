using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;
using static ShopAI.Application.Handlers.ToggleFavoriteCommandHandler;

namespace ShopAI.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces(MediaTypeNames.Application.Json)]
    public class FavoritesController(IMediator mediator) : ControllerBase
    {
        /// <summary>
        /// Получить список избранных товаров текущего пользователя.
        /// </summary>
        /// <response code="200">Список избранных товаров.</response>
        /// <response code="401">Пользователь не авторизован.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<ProductShortDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ProductShortDto>>> GetFavorites()
        {
            var result = await mediator.Send(new GetFavoritesQuery());
            return Ok(result);
        }

        /// <summary>
        /// Добавить товар в избранное или удалить из него (Toggle).
        /// </summary>
        /// <remarks>
        /// Если товара не было в избранном, он будет добавлен (вернется true). 
        /// Если товар уже был в избранном, он будет удален (вернется false).
        /// </remarks>
        /// <param name="productId">Идентификатор товара.</param>
        /// <response code="200">Возвращает статус: true - добавлен, false - удален.</response>
        /// <response code="404">Товар с указанным ID не найден.</response>
        [HttpPost("{productId:guid}/toggle")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<bool>> ToggleFavorite(Guid productId)
        {
            var isAdded = await mediator.Send(new ToggleFavoriteCommand(productId));
            return Ok(new { isAdded });
        }

        /// <summary>
        /// Добавить AI-бандл товаров в избранное.
        /// </summary>
        /// <remarks>
        /// Передайте productIds из выбранного элемента bundles, который вернул /api/ai/shopping-assistant.
        /// В отличие от toggle, этот метод только добавляет товары: уже избранные товары остаются в избранном и возвращаются в alreadyInFavoritesProductIds.
        /// </remarks>
        /// <param name="command">Список товаров бандла, которые нужно добавить в избранное.</param>
        /// <response code="200">Бандл обработан, отсутствующие товары добавлены в избранное.</response>
        /// <response code="400">Список товаров пустой.</response>
        /// <response code="401">Пользователь не авторизован.</response>
        /// <response code="404">Один из товаров бандла не найден.</response>
        [HttpPost("bundles")]
        [ProducesResponseType(typeof(AddBundleToFavoritesResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AddBundleToFavoritesResult>> AddBundle([FromBody] AddBundleToFavoritesCommand command)
        {
            try
            {
                var result = await mediator.Send(command);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
