namespace ShopAI.Application.Models;

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

public record ShoppingAssistantResponse(
    InterpretedShoppingQuery Interpreted,
    List<ProductShortDto> Items,
    List<List<ProductShortDto>> Bundles);

public record GenerateProductTagsRequest(
    string Name,
    string? Description,
    Dictionary<string, string>? Attributes,
    int? Limit);

public record GenerateProductTagsResponse(List<string> Tags);
