using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на создание магазина текущего пользователя.
/// </summary>
/// <param name="Name">Название магазина.</param>
/// <param name="Description">Описание магазина.</param>
/// <param name="LogoPath">Путь или URL логотипа магазина.</param>
/// <param name="UrlAlias">Короткий уникальный псевдоним магазина для URL.</param>
public record CreateShopCommand(string Name, string Description, string LogoPath, string UrlAlias) : IRequest<Guid>;

public class CreateShopHandler(
    IShopRepository shopRepository,
    IUserRepository userRepository,
    IUserContext userContext) : IRequestHandler<CreateShopCommand, Guid>
{
    public async Task<Guid> Handle(CreateShopCommand request, CancellationToken ct)
    {
        // 1. Получаем ID текущего юзера из контекста (из токена)
        var ownerId = userContext.UserId;

        // 2. Проверка уникальности Alias
        var existingShop = await shopRepository.GetByUrlAliasAsync(request.UrlAlias);
        if (existingShop != null)
        {
            throw new InvalidOperationException("Этот URL-адрес уже занят другим магазином.");
        }

        // 3. Создание сущности
        var shop = new Shop(request.Name, request.Description, request.LogoPath, request.UrlAlias, ownerId);

        await shopRepository.AddAsync(shop);
        await shopRepository.SaveAsync(ct);

        return shop.Id;
    }
}
