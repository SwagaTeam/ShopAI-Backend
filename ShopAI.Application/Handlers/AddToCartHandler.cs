using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Команда для добавления товара в корзину.
/// </summary>
/// <param name="ProductId">ID товара, который нужно добавить.</param>
/// <param name="Quantity">Количество (по умолчанию 1).</param>
public record AddToCartCommand(Guid ProductId, int Quantity = 1) : IRequest<Guid>;

public class AddToCartHandler(
    ICartRepository cartRepository,
    IProductRepository productRepository,
    IUserContext userContext) : IRequestHandler<AddToCartCommand, Guid>
{
    public async Task<Guid> Handle(AddToCartCommand request, CancellationToken ct)
    {
        var userId = userContext.UserId;

        // 1. Проверяем существование товара
        var product = await productRepository.GetByIdAsync(request.ProductId)
            ?? throw new KeyNotFoundException("Товар не найден.");

        // 2. Ищем корзину пользователя или создаем новую
        var cart = await cartRepository.GetByUserIdAsync(userId, ct);
        if (cart == null)
        {
            cart = new Cart { UserId = userId, UpdatedAtUtc = DateTime.UtcNow };
            await cartRepository.AddAsync(cart);
            await cartRepository.SaveAsync(ct);
        }

        // 3. Проверяем, есть ли уже такой товар в корзине
        var existingItem = await cartRepository.GetItemAsync(cart.Id, request.ProductId, ct);

        if (existingItem != null)
        {
            // Если есть — увеличиваем количество
            existingItem.Quantity += request.Quantity;
            cartRepository.UpdateItem(existingItem);
        }
        else
        {
            // Если нет — создаем новую запись
            var newItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity
            };
            await cartRepository.AddItemAsync(newItem);
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await cartRepository.SaveAsync(ct);

        return cart.Id;
    }
}