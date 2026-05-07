using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на получение корзины текущего пользователя.
/// </summary>
public record GetCartQuery : IRequest<CartVm>;

public class GetCartQueryHandler(
    ICartRepository cartRepository,
    IUserContext userContext) : IRequestHandler<GetCartQuery, CartVm>
{
    public async Task<CartVm> Handle(GetCartQuery request, CancellationToken ct)
    {
        // Получаем ID пользователя из JWT через наш контекст
        var userId = userContext.UserId;

        // Получаем корзину со всеми айтемами и данными продуктов (Eager Loading через Include)
        var cart = await cartRepository.GetByUserIdAsync(userId, ct);

        // Если корзины нет в БД, возвращаем пустую модель (стандарт для UI)
        if (cart == null)
        {
            return new CartVm(Guid.Empty, new List<CartItemVm>(), 0);
        }

        // Маппим сущности БД в твои CartItemVm
        var itemVms = cart.Items.Select(item => new CartItemVm(
            item.ProductId,
            item.Product?.Name ?? "Товар не найден",
            item.Quantity,
            item.Product?.Price ?? 0,
            item.Product?.ImageUrl ?? string.Empty
        )).ToList();

        // Считаем общую сумму всех товаров в корзине
        var totalPrice = itemVms.Sum(x => x.Price * x.Quantity);

        return new CartVm(cart.Id, itemVms, totalPrice);
    }
}