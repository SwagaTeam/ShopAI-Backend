using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Команда для добавления AI-бандла товаров в корзину.
/// </summary>
/// <param name="ProductIds">Идентификаторы товаров из выбранного бандла, например из bundles ответа /api/ai/shopping-assistant.</param>
/// <param name="Quantity">Количество каждого товара, которое нужно добавить в корзину. По умолчанию 1.</param>
public record AddBundleToCartCommand(List<Guid> ProductIds, int Quantity = 1) : IRequest<AddBundleToCartResult>;

/// <summary>
/// Команда для добавления AI-бандла товаров в избранное.
/// </summary>
/// <param name="ProductIds">Идентификаторы товаров из выбранного бандла, например из bundles ответа /api/ai/shopping-assistant.</param>
public record AddBundleToFavoritesCommand(List<Guid> ProductIds) : IRequest<AddBundleToFavoritesResult>;

public class BundleActionsCommandHandler(
    ICartRepository cartRepository,
    IFavoriteRepository favoriteRepository,
    IProductRepository productRepository,
    IUserContext userContext)
    : IRequestHandler<AddBundleToCartCommand, AddBundleToCartResult>,
      IRequestHandler<AddBundleToFavoritesCommand, AddBundleToFavoritesResult>
{
    public async Task<AddBundleToCartResult> Handle(AddBundleToCartCommand request, CancellationToken ct)
    {
        var productIds = await ValidateProductIdsAsync(request.ProductIds, ct);
        if (request.Quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.");

        var userId = userContext.UserId;
        var cart = await cartRepository.GetByUserIdAsync(userId, ct);
        if (cart == null)
        {
            cart = new Cart { UserId = userId, UpdatedAtUtc = DateTime.UtcNow };
            await cartRepository.AddAsync(cart);
        }

        foreach (var productId in productIds)
        {
            var existingItem = await cartRepository.GetItemAsync(cart.Id, productId, ct);
            if (existingItem == null)
            {
                await cartRepository.AddItemAsync(new CartItem
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = request.Quantity
                });
            }
            else
            {
                existingItem.Quantity += request.Quantity;
                cartRepository.UpdateItem(existingItem);
            }
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await cartRepository.SaveAsync(ct);

        return new AddBundleToCartResult(cart.Id, productIds, request.Quantity, productIds.Count);
    }

    public async Task<AddBundleToFavoritesResult> Handle(AddBundleToFavoritesCommand request, CancellationToken ct)
    {
        var productIds = await ValidateProductIdsAsync(request.ProductIds, ct);
        var userId = userContext.UserId;

        var addedProductIds = new List<Guid>();
        var alreadyInFavorites = new List<Guid>();

        foreach (var productId in productIds)
        {
            var existingFavorite = await favoriteRepository.GetFavoriteAsync(userId, productId, ct);
            if (existingFavorite != null)
            {
                alreadyInFavorites.Add(productId);
                continue;
            }

            await favoriteRepository.AddAsync(new FavoriteProduct
            {
                UserId = userId,
                ProductId = productId,
                AddedAtUtc = DateTime.UtcNow
            });
            addedProductIds.Add(productId);
        }

        await favoriteRepository.SaveAsync(ct);
        return new AddBundleToFavoritesResult(productIds, addedProductIds, alreadyInFavorites);
    }

    private async Task<List<Guid>> ValidateProductIdsAsync(List<Guid>? productIds, CancellationToken ct)
    {
        var normalized = (productIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
            throw new ArgumentException("productIds must contain at least one product id.");

        foreach (var productId in normalized)
        {
            if (!await productRepository.ExistsAsync(productId, ct))
                throw new KeyNotFoundException($"Product with ID {productId} was not found.");
        }

        return normalized;
    }
}
