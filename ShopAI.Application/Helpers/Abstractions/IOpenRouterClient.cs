using ShopAI.Application.Models;

namespace ShopAI.Application.Helpers.Abstractions;

public interface IOpenRouterClient
{
    Task<InterpretedShoppingQuery?> InterpretShoppingPromptAsync(string userPrompt, CancellationToken ct = default);
    Task<List<string>?> GenerateProductTagsAsync(
        string name,
        string? description,
        Dictionary<string, string>? attributes,
        int limit,
        CancellationToken ct = default);
}
