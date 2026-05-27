using ShopAI.Application.Models;

namespace ShopAI.Application.Helpers.Abstractions;

public interface IOpenRouterClient
{
    Task<InterpretedShoppingQuery?> InterpretShoppingPromptAsync(string userPrompt, CancellationToken ct = default);
}
