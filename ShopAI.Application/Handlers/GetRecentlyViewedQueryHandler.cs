using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetRecentlyViewedQuery(int Limit = 10) : IRequest<List<ProductShortDto>>;

    public class GetRecentlyViewedHandler(
        IRecentlyViewedRepository historyRepository,
        IUserContext userContext,
        AutoMapper.IMapper mapper) : IRequestHandler<GetRecentlyViewedQuery, List<ProductShortDto>>
    {
        public async Task<List<ProductShortDto>> Handle(GetRecentlyViewedQuery request, CancellationToken ct)
        {
            var products = await historyRepository.GetHistoryAsync(userContext.UserId, request.Limit, ct);
            return mapper.Map<List<ProductShortDto>>(products);
        }
    }
}
