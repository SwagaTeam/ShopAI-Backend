using Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Helpers.Abstractions;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Seller,Admin")]
public class UsersController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    /// <summary>
    /// Получить профиль текущего авторизованного пользователя.
    /// </summary>
    /// <remarks>
    /// ID пользователя берется автоматически из JWT-токена (Claim: sub/nameidentifier).
    /// </remarks>
    [HttpGet("current")]
    [ProducesResponseType(typeof(CustomerInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerInfo>> GetMe()
    {
        var query = new GetCurrentUserQuery(userContext.UserId);
        var user = await mediator.Send(query);

        return Ok(user);
    }
}
