using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    /// <summary>
    /// Команда для добавления отзыва на товар.
    /// </summary>
    public record AddProductReviewCommand(Guid ProductId, int Rating, string Comment) : IRequest<Guid>;

    public class AddProductReviewCommandHandler(
    IProductReviewRepository reviewRepository,
    IProductRepository productRepository,
    IUserContext userContext) : IRequestHandler<AddProductReviewCommand, Guid>
    {
        public async Task<Guid> Handle(AddProductReviewCommand request, CancellationToken ct)
        {
            // 1. Валидация оценки
            if (request.Rating < 1 || request.Rating > 5)
                throw new ArgumentException("Оценка должна быть от 1 до 5.");

            // 2. Проверка существования товара
            var productExists = await productRepository.GetByIdAsync(request.ProductId) != null;
            if (!productExists)
                throw new KeyNotFoundException("Товар не найден.");

            var userId = userContext.UserId;

            // 3. Проверка на дубликат отзыва
            var alreadyReviewed = await reviewRepository.HasUserReviewedProductAsync(userId, request.ProductId, ct);
            if (alreadyReviewed)
                throw new InvalidOperationException("Вы уже оставили отзыв на этот товар.");

            // 4. Создание отзыва
            var review = new ProductReview
            {
                UserId = userId,
                ProductId = request.ProductId,
                Rating = request.Rating,
                Comment = request.Comment.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await reviewRepository.AddAsync(review);
            await reviewRepository.SaveAsync(ct);

            return review.Id;
        }
    }
}
