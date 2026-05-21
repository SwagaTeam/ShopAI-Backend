using AutoMapper;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetMyShopsQuery() : IRequest<List<ShopDto>>;

    public class GetMyShopsHandler(
    IShopRepository shopRepository,
    IUserContext userContext,
    IMapper mapper) : IRequestHandler<GetMyShopsQuery, List<ShopDto>>
    {
        public async Task<List<ShopDto>> Handle(GetMyShopsQuery request, CancellationToken ct)
        {
            // 1. Получаем ID пользователя из JWT-токена
            var userId = userContext.UserId;

            // 2. Достаем его магазины из БД
            var shops = await shopRepository.GetByOwnerIdAsync(userId, ct);

            // 3. Возвращаем DTO
            return mapper.Map<List<ShopDto>>(shops);
        }
    }
}
