using AutoMapper;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetFavoritesQuery() : IRequest<List<ProductShortDto>>;

    public class GetFavoritesQueryHandler(
        IFavoriteRepository favoriteRepository,
        IUserContext userContext,
        IMapper mapper) : IRequestHandler<GetFavoritesQuery, List<ProductShortDto>>
    {
        public async Task<List<ProductShortDto>> Handle(GetFavoritesQuery request, CancellationToken ct)
        {
            var products = await favoriteRepository.GetUserFavoritesAsync(userContext.UserId, ct);
            return mapper.Map<List<ProductShortDto>>(products);
        }
    }
}
