using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    /// <summary>
    /// Команда для фиксации просмотра товара пользователем.
    /// </summary>
    public record TrackProductViewCommand(Guid ProductId) : IRequest<Unit>;

    public class TrackProductViewCommandHandler(
    IRecentlyViewedRepository historyRepository,
    IProductRepository productRepository,
    IUserContext userContext) : IRequestHandler<TrackProductViewCommand, Unit>
    {
        public async Task<Unit> Handle(TrackProductViewCommand request, CancellationToken ct)
        {
            // Проверяем, существует ли товар (чтобы не засорять историю несуществующими ID)
            var productExists = await productRepository.GetByIdAsync(request.ProductId) != null;
            if (!productExists)
                throw new KeyNotFoundException("Товар не найден.");

            await historyRepository.TrackViewAsync(userContext.UserId, request.ProductId, ct);

            return Unit.Value;
        }
    }
}
