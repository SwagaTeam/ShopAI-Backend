using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record GetShopByIdQuery(Guid Id) : IRequest<ShopDto>;

public class GetShopByIdHandler(
    IShopRepository shopRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetShopByIdQuery, ShopDto>
{
    public async Task<ShopDto> Handle(GetShopByIdQuery request, CancellationToken ct)
    {
        // 1. Поиск магазина
        var shop = await shopRepository.GetByIdAsync(request.Id);

        if (shop == null)
        {
            throw new KeyNotFoundException($"Магазин с ID {request.Id} не найден.");
        }

        // 2. Опционально: подгружаем имя владельца, если это нужно для UI
        var owner = await userRepository.GetByIdAsync(shop.OwnerId);

        // 3. Маппинг сущности на ViewModel
        return new ShopDto(
            shop.Id,
            shop.Name,
            shop.UrlAlias,
            shop.OwnerId,
            owner?.FullName ?? "Неизвестный владелец"
        );
    }
}