using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на получение корзины текущего пользователя.
/// </summary>
public record GetCartQuery : IRequest<CartVm>;

public class GetCartQueryHandler(
    ICartRepository cartRepository,
    IUserContext userContext,
    IFileStorageService fileStorageService,
    IConfiguration configuration) : IRequestHandler<GetCartQuery, CartVm>
{
    public async Task<CartVm> Handle(GetCartQuery request, CancellationToken ct)
    {
        var userId = userContext.UserId;
        var cart = await cartRepository.GetByUserIdAsync(userId, ct);

        if (cart == null)
        {
            return new CartVm(Guid.Empty, new List<CartItemVm>(), 0);
        }

        var invalidItems = cart.Items.Where(item => item.Quantity <= 0).ToList();
        if (invalidItems.Count > 0)
        {
            foreach (var item in invalidItems)
                cartRepository.RemoveItem(item);

            cart.UpdatedAtUtc = DateTime.UtcNow;
            await cartRepository.SaveAsync(ct);
        }

        var validItems = cart.Items.Where(item => item.Quantity > 0).ToList();
        var itemVms = new List<CartItemVm>(validItems.Count);
        foreach (var item in validItems)
        {
            itemVms.Add(new CartItemVm(
                item.ProductId,
                item.Product?.Name ?? "Товар не найден",
                item.Quantity,
                item.Product?.Price ?? 0,
                await ResolveUrlAsync(item.Product?.ImageUrl ?? string.Empty)
            ));
        }

        var totalPrice = itemVms.Sum(x => x.Price * x.Quantity);
        return new CartVm(cart.Id, itemVms, totalPrice);
    }

    private async Task<string> ResolveUrlAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;

        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        return await fileStorageService.GetPresignedUrlAsync(bucket, value);
    }
}
