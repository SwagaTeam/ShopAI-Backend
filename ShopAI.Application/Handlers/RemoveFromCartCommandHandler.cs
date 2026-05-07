using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Команда для удаления товара из корзины.
/// </summary>
/// <param name="ProductId">Идентификатор товара, который нужно убрать.</param>
public record RemoveFromCartCommand(Guid ProductId) : IRequest<Unit>;

public class RemoveFromCartHandler(
    ICartRepository cartRepository,
    IUserContext userContext) : IRequestHandler<RemoveFromCartCommand, Unit>
{
    public async Task<Unit> Handle(RemoveFromCartCommand request, CancellationToken ct)
    {
        var userId = userContext.UserId;

        // 1. Находим корзину пользователя
        var cart = await cartRepository.GetByUserIdAsync(userId, ct);

        if (cart == null)
        {
            throw new KeyNotFoundException("Корзина не найдена.");
        }

        // 2. Ищем конкретный элемент в этой корзине
        var item = await cartRepository.GetItemAsync(cart.Id, request.ProductId, ct);

        if (item != null)
        {
            // 3. Удаляем элемент через репозиторий
            cartRepository.RemoveItem(item);

            // Обновляем время изменения корзины
            cart.UpdatedAtUtc = DateTime.UtcNow;

            await cartRepository.SaveAsync(ct);
        }

        return Unit.Value;
    }
}