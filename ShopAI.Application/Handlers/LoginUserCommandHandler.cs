using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на вход пользователя.
/// </summary>
/// <param name="Email">Email, указанный при регистрации.</param>
/// <param name="Password">Пароль пользователя.</param>
public record LoginUserCommand(string Email, string Password) : IRequest<AuthResponse>;

public class LoginUserCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtProvider jwtProvider,
    IConfiguration configuration)
    : IRequestHandler<LoginUserCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.ToLower(), ct);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Неверный email или пароль.");
        }

        var isPasswordValid = passwordHasher.Verify(request.Password, user.Password, user.Salt);
        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Неверный email или пароль.");
        }

        if (IsConfiguredAdminEmail(user.Email) && user.Role != User.AdminRole)
        {
            user.Role = User.AdminRole;
        }

        var tokens = jwtProvider.GenerateTokens(user);

        var refreshToken = new RefreshToken
        {
            Token = tokens.RefreshToken,
            UserId = user.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await refreshTokenRepository.AddAsync(refreshToken);
        await refreshTokenRepository.SaveAsync(ct);

        return new AuthResponse(tokens.AccessToken, tokens.RefreshToken);
    }

    private bool IsConfiguredAdminEmail(string email)
    {
        var adminEmail = configuration["Admin:Email"];
        return !string.IsNullOrWhiteSpace(adminEmail)
               && string.Equals(email.Trim(), adminEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
