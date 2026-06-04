using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetProductReviewsQuery(Guid ProductId, int Page = 1, int PageSize = 10) : IRequest<List<ProductReviewDto>>;

    public class GetProductReviewsHandler(
        IProductReviewRepository reviewRepository,
        AutoMapper.IMapper mapper) : IRequestHandler<GetProductReviewsQuery, List<ProductReviewDto>>
    {
        public async Task<List<ProductReviewDto>> Handle(GetProductReviewsQuery request, CancellationToken ct)
        {
            if (request.Page < 1)
                throw new ArgumentException("Номер страницы должен быть больше нуля.");

            var skip = (request.Page - 1) * request.PageSize;

            var reviews = await reviewRepository.GetByProductIdAsync(request.ProductId, skip, request.PageSize, ct);

            // В маппинге укажи: UserName = src.User.FullName
            return mapper.Map<List<ProductReviewDto>>(reviews);
        }
    }
}
