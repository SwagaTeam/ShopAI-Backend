namespace ShopAI.Application.Models;

/// <summary>
/// Результат добавления AI-бандла в корзину.
/// </summary>
/// <param name="CartId">Идентификатор корзины текущего пользователя.</param>
/// <param name="ProductIds">Идентификаторы товаров, добавленных в корзину.</param>
/// <param name="QuantityPerProduct">Количество, добавленное для каждого товара.</param>
/// <param name="ItemsAffectedCount">Количество позиций бандла, обработанных в корзине.</param>
public record AddBundleToCartResult(
    Guid CartId,
    List<Guid> ProductIds,
    int QuantityPerProduct,
    int ItemsAffectedCount);

/// <summary>
/// Результат добавления AI-бандла в избранное.
/// </summary>
/// <param name="ProductIds">Все идентификаторы товаров из запроса после удаления дублей.</param>
/// <param name="AddedProductIds">Товары, которые были добавлены в избранное этим запросом.</param>
/// <param name="AlreadyInFavoritesProductIds">Товары, которые уже были в избранном и не добавлялись повторно.</param>
public record AddBundleToFavoritesResult(
    List<Guid> ProductIds,
    List<Guid> AddedProductIds,
    List<Guid> AlreadyInFavoritesProductIds);
