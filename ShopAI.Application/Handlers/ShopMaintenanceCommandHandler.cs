using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

public record UpdateShopCommand(Guid Id, string Name, string UrlAlias) : IRequest<Shop>;
public record DeleteShopCommand(Guid Id) : IRequest<Guid>;

public class ShopMaintenanceHandler(
    IShopRepository shopRepository,
    IUserContext userContext)
    : IRequestHandler<UpdateShopCommand, Shop>,
      IRequestHandler<DeleteShopCommand, Guid>
{
    public async Task<Shop> Handle(UpdateShopCommand request, CancellationToken ct)
    {
        var shop = await shopRepository.GetByIdAsync(request.Id)
                   ?? throw new KeyNotFoundException("Магазин не найден.");

        // Проверка прав (только владелец может менять)
        if (shop.OwnerId != userContext.UserId)
            throw new UnauthorizedAccessException("У вас нет прав на редактирование этого магазина.");

        // Проверка уникальности нового Alias, если он изменился
        if (shop.UrlAlias != request.UrlAlias.ToLower())
        {
            var duplicate = await shopRepository.GetByUrlAliasAsync(request.UrlAlias);
            if (duplicate != null)
                throw new InvalidOperationException("Новый URL-адрес уже занят.");
        }

        shop.Name = request.Name;
        shop.UrlAlias = request.UrlAlias.ToLower();

        shopRepository.Update(shop);
        await shopRepository.SaveAsync(ct);
        return shop;
    }

    public async Task<Guid> Handle(DeleteShopCommand request, CancellationToken ct)
    {
        var shop = await shopRepository.GetByIdAsync(request.Id)
                   ?? throw new KeyNotFoundException("Магазин не найден.");

        if (shop.OwnerId != userContext.UserId)
            throw new UnauthorizedAccessException("У вас нет прав на удаление этого магазина.");

        shopRepository.Delete(shop);
        await shopRepository.SaveAsync(ct);
        return request.Id;
    }
}