namespace ShopAI.Application.Models;
/// <summary>
/// Запрос на обновление основных данных магазина.
/// </summary>
/// <param name="Name">Новое название магазина.</param>
/// <param name="UrlAlias">Новый короткий уникальный псевдоним магазина для URL.</param>
public record UpdateShopRequest(string Name, string UrlAlias);
