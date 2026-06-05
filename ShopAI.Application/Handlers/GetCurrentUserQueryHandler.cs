using Domain.ValueObjects;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetCurrentUserQuery(Guid UserId) : IRequest<CustomerInfo>;

    public class GetCurrentUserHandler(IUserRepository userRepository)
        : IRequestHandler<GetCurrentUserQuery, CustomerInfo>
    {
        public async Task<CustomerInfo> Handle(GetCurrentUserQuery request, CancellationToken ct)
        {
            var user = await userRepository.GetByIdAsync(request.UserId)
                       ?? throw new KeyNotFoundException("Пользователь не найден.");

            return new CustomerInfo(user.Id, user.FullName, user.Email, user.Phone, user.Role);
        }
    }
}
