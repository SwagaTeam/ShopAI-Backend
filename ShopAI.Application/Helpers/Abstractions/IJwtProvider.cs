using Domain.Entities;
using ShopAI.Application.Models;

namespace ShopAI.Application.Helpers.Abstractions
{
    public interface IJwtProvider
    {
        TokenResponse GenerateTokens(User user);
        string GenerateRefreshToken();
    }
}
