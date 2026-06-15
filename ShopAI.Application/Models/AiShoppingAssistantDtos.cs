namespace ShopAI.Application.Models;

/// <summary>
/// Запрос к AI-помощнику по подбору товаров.
/// </summary>
/// <param name="UserPrompt">Свободный текстовый запрос пользователя с описанием потребности.</param>
/// <param name="BudgetMin">Необязательная нижняя граница бюджета.</param>
/// <param name="BudgetMax">Необязательная верхняя граница бюджета.</param>
/// <param name="CategoryId">Необязательный идентификатор категории, в рамках которой нужно искать товары.</param>
/// <param name="Limit">Необязательный лимит количества товаров в ответе.</param>
public record ShoppingAssistantRequest(
    string UserPrompt,
    decimal? BudgetMin,
    decimal? BudgetMax,
    Guid? CategoryId,
    int? Limit);

public record InterpretedShoppingQuery
{
    public string Intent { get; init; } = "unknown";
    public List<string> CategoryHints { get; init; } = [];
    public List<string> RequiredCategories { get; init; } = [];
    public List<string> Keywords { get; init; } = [];
    public List<string> Colors { get; init; } = [];
    public List<string> Brands { get; init; } = [];
    public List<string> Tags { get; init; } = [];
    public Dictionary<string, string> Attributes { get; init; } = [];
    public decimal? BudgetMin { get; init; }
    public decimal? BudgetMax { get; init; }
    public string PriceSort { get; init; } = "none";
    public int? BundleSize { get; init; }
}

/// <summary>
/// Ответ AI-помощника по подбору товаров.
/// </summary>
/// <param name="Interpreted">Структурированная интерпретация пользовательского запроса.</param>
/// <param name="Items">Подходящие отдельные товары.</param>
/// <param name="Bundles">Подборки товаров, которые AI предлагает покупать вместе. ProductIds из выбранного бандла можно отправить в /api/Cart/bundles или /api/Favorites/bundles.</param>
public record ShoppingAssistantResponse(
    InterpretedShoppingQuery Interpreted,
    List<ProductShortDto> Items,
    List<List<ProductShortDto>> Bundles)
{
    public string Message => Items.Count == 0 && Bundles.Count == 0 ? "Ничего не найдено" : string.Empty;
}

/// <summary>
/// Запрос на генерацию тегов товара.
/// </summary>
/// <param name="Name">Название товара. Обязательное поле.</param>
/// <param name="Description">Необязательное описание товара для более точной генерации тегов.</param>
/// <param name="Attributes">Необязательные характеристики товара в формате ключ-значение.</param>
/// <param name="Limit">Необязательное максимальное количество тегов в ответе.</param>
public record GenerateProductTagsRequest(
    string Name,
    string? Description,
    Dictionary<string, string>? Attributes,
    int? Limit);

public record GenerateProductTagsResponse(List<string> Tags);
