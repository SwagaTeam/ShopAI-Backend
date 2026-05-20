using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
     /// <summary>
     /// Команда для переключения статуса "В избранном".
     /// </summary>
     public record ToggleFavoriteCommand(Guid ProductId) : IRequest<bool>;

     public class ToggleFavoriteCommandHandler(
         IFavoriteRepository favoriteRepository,
         IProductRepository productRepository,
         IUserContext userContext) : IRequestHandler<ToggleFavoriteCommand, bool>
     {
         public async Task<bool> Handle(ToggleFavoriteCommand request, CancellationToken ct)
         {
             var userId = userContext.UserId;

             // Проверяем, существует ли вообще такой товар
             var product = await productRepository.GetByIdAsync(request.ProductId);
             if (product == null)
                 throw new KeyNotFoundException("Товар не найден.");

             // Ищем запись в избранном
             var existingFavorite = await favoriteRepository.GetFavoriteAsync(userId, request.ProductId, ct);

             bool isAdded;

             if (existingFavorite != null)
             {
                 // Если есть - удаляем
                 favoriteRepository.Delete(existingFavorite);
                 isAdded = false;
             }
             else
             {
                 // Если нет - добавляем
                 var newFavorite = new FavoriteProduct
                 {
                     UserId = userId,
                     ProductId = request.ProductId,
                     AddedAtUtc = DateTime.UtcNow
                 };
                 await favoriteRepository.AddAsync(newFavorite);
                 isAdded = true;
             }

             await favoriteRepository.SaveAsync(ct);
             return isAdded;
         }
     }
}
