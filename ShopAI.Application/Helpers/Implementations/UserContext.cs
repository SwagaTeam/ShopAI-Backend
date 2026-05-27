using System.Security.Claims;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using ShopAI.Application.Helpers.Abstractions;

namespace ShopAI.Application.Helpers.Implementations
{
    public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
    {
        public Guid UserId
        {
            get
            {
                var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)
                                  ?? httpContextAccessor.HttpContext?.User?.FindFirst("sub");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    throw new UnauthorizedAccessException("Пользователь не авторизован.");
                }

                return userId;
            }
        }

        public string? Role => httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;

        public bool IsAdmin => Role == User.AdminRole;
    }
}
