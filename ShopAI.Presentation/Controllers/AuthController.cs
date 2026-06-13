using System.Net.Mime;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Регистрация нового пользователя.
    /// </summary>
    /// <param name="command">Данные регистрации: имя, email, телефон и пароль.</param>
    /// <returns>Идентификатор созданного пользователя.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Register([FromBody] RegisterUserCommand command)
    {
        var userId = await mediator.Send(command);
        return Ok(userId);
    }

    /// <summary>
    /// Вход в систему (получение пары Access и Refresh токенов).
    /// </summary>
    /// <param name="command">Учетные данные пользователя: email и пароль.</param>
    /// <returns>Пара access и refresh токенов.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginUserCommand command)
    {
        var response = await mediator.Send(command);

        return Ok(response);
    }

    /// <summary>
    /// Обновление Access токена с помощью Refresh токена.
    /// </summary>
    /// <remarks>
    /// Позволяет получить новую пару токенов, если срок действия Access токена истек.
    /// Старый Refresh токен будет отозван (Rotational Refresh Tokens).
    /// </remarks>
    /// <param name="command">Refresh-токен, который нужно обменять на новую пару токенов.</param>
    /// <returns>Новая пара access и refresh токенов.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenCommand command)
    {
        var response = await mediator.Send(command);

        return Ok(response);
    }
}
