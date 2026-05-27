using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;

namespace ShopAI.Application.Helpers.Implementations;

public class OpenRouterClient(HttpClient httpClient, IConfiguration configuration) : IOpenRouterClient
{
    public async Task<InterpretedShoppingQuery?> InterpretShoppingPromptAsync(string userPrompt, CancellationToken ct = default)
    {
        var apiKey = configuration["OpenRouter:ApiKey"];
        var model = configuration["OpenRouter:Model"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            return null;

        var systemPrompt = """
Return only strict JSON object. No markdown.
Schema:
{
  "intent":"search|bundle|gift|compare|unknown",
  "categoryHints":[],
  "requiredCategories":[],
  "keywords":[],
  "colors":[],
  "brands":[],
  "tags":[],
  "attributes":{},
  "budgetMin":null,
  "budgetMax":null,
  "priceSort":"asc|desc|none",
  "bundleSize":null
}
""";

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            return JsonSerializer.Deserialize<InterpretedShoppingQuery>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
