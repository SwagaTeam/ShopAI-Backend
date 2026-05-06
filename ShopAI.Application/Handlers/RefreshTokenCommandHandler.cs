using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record RefreshTokenCommand(string RefreshToken) : IRequest<TokenResponse>;

    public class RefreshTokenHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IJwtProvider jwtProvider) : IRequestHandler<RefreshTokenCommand, TokenResponse>
    {
        public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
        {
            var storedToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);

            if (storedToken == null || storedToken.ExpiryDate < DateTime.UtcNow || storedToken.IsRevoked)
            {
                throw new UnauthorizedAccessException("Токен невалиден или протух.");
            }

            // Генерируем новую пару
            var tokens = jwtProvider.GenerateTokens(storedToken.User);

            // Ротация: старый токен отзываем
            storedToken.IsRevoked = true;

            // Добавляем новый
            await refreshTokenRepository.AddAsync(new RefreshToken
            {
                Token = tokens.RefreshToken,
                UserId = storedToken.UserId,
                ExpiryDate = DateTime.UtcNow.AddDays(7)
            });

            await refreshTokenRepository.SaveAsync(ct);
            return tokens;
        }
    }
}
