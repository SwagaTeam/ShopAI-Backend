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
                var user = httpContextAccessor.HttpContext?.User;

                var userIdClaim =
                    user?.FindFirst("sub") ??
                    user?.FindFirst(ClaimTypes.NameIdentifier) ??
                    user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    var claims = user?.Claims.Select(c => $"{c.Type} = {c.Value}");

                    Console.WriteLine(string.Join("\n", claims ?? []));
                    throw new UnauthorizedAccessException("Пользователь не авторизован.");
                }   

                return userId;
            }
        }

        public string? Role => httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;

        public bool IsAdmin => Role == User.AdminRole;
    }
}
