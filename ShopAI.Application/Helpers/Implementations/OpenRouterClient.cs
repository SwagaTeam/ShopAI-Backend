using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;

namespace ShopAI.Application.Helpers.Implementations;

public class OpenRouterClient(HttpClient httpClient, IConfiguration configuration) : IOpenRouterClient
{
    public async Task<InterpretedShoppingQuery?> InterpretShoppingPromptAsync(string userPrompt,
        CancellationToken ct = default)
    {
        var apiKey = configuration["OpenRouter:ApiKey"];
        var primaryModel = configuration["OpenRouter:Model"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(primaryModel))
            return null;

        var fallbackModelsRaw = configuration["OpenRouter:FallbackModels"]
                                ??
                                "google/gemini-2.0-flash-exp:free,meta-llama/llama-3.1-8b-instruct:free,mistralai/mistral-7b-instruct:free";
        var fallbackModels = fallbackModelsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var maxTokens = TryParseInt(configuration["OpenRouter:MaxTokens"], 500);
        var temperature = TryParseDouble(configuration["OpenRouter:Temperature"], 0.1);
        var topP = TryParseDouble(configuration["OpenRouter:TopP"], 1.0);

        var models = new List<string> { primaryModel };
        models.AddRange(fallbackModels.Where(m => !string.Equals(m, primaryModel, StringComparison.OrdinalIgnoreCase)));

        var systemPrompt = """
                           You are a precise e-commerce search assistant. Your task is to extract product search parameters from user input into a strict JSON object.

                           Rules:
                           1. If a brand is mentioned, put it in "brands".
                           2. If a color is mentioned, put it in "colors".
                           3. Extract any specific features or descriptions as "tags".
                           4. If the user mentions a category, add it to "categoryHints" or "requiredCategories".
                           5. If the user mentions a price range or budget, fill "budgetMin"/"budgetMax".
                           6. If you are unsure about a field, leave it empty or null (do not hallucinate).
                           7. Return ONLY JSON. No markdown, no explanations.

                           Schema:
                           {
                             "intent": "search|bundle|gift|compare|unknown",
                             "categoryHints": ["list", "of", "strings"],
                             "requiredCategories": ["list", "of", "strings"],
                             "keywords": ["list", "of", "strings"],
                             "colors": ["list", "of", "strings"],
                             "brands": ["list", "of", "strings"],
                             "tags": ["list", "of", "strings"],
                             "attributes": {"key": "value"},
                             "budgetMin": null,
                             "budgetMax": null,
                             "priceSort": "asc|desc|none",
                             "bundleSize": null
                           }
                           """;

        var payload = new
        {
            model = primaryModel, 
            max_tokens = maxTokens,
            temperature,
            top_p = topP,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[]
                    {
                        new { type = "text", text = systemPrompt }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = userPrompt }
                    }
                }
            },
            response_format = new { type = "json_object" }
        };

        var result = await SendWithRetryAsync(payload, apiKey, ct);

        try
        {
            return JsonSerializer.Deserialize<InterpretedShoppingQuery>(result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
        }

        return null;
    }

    private static int TryParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static double TryParseDouble(string? value, double defaultValue)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private async Task<string?> SendWithRetryAsync(object payload, string apiKey, CancellationToken ct)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = JsonContent.Create(payload);

            using var response = await httpClient.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var content = ExtractContent(doc.RootElement);
                if (string.IsNullOrWhiteSpace(content)) return null;
                return ExtractJson(content);
            }

            if ((int)response.StatusCode != 429 || attempt == maxAttempts) return null;

            var delay = GetRetryDelay(response, attempt);
            await Task.Delay(delay, ct);
        }

        return null;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero) return delta;

        if (retryAfter?.Date is DateTimeOffset when)
        {
            var wait = when - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }

        return TimeSpan.FromSeconds(Math.Min(2 * attempt, 8));
    }

    private static string? ExtractContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
            return null;

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var contentElement))
            return null;

        if (contentElement.ValueKind == JsonValueKind.String)
            return contentElement.GetString();

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in contentElement.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                {
                    sb.Append(part.GetString());
                    continue;
                }

                if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String) sb.Append(text.GetString());
            }

            return sb.ToString();
        }

        return contentElement.ToString();
    }

    private static string? ExtractJson(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            return trimmed;

        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var firstNewLine = trimmed.IndexOf('\n', fenceStart);
            if (firstNewLine > fenceStart)
            {
                var fenceEnd = trimmed.IndexOf("```", firstNewLine + 1, StringComparison.Ordinal);
                if (fenceEnd > firstNewLine)
                {
                    var inside = trimmed.Substring(firstNewLine + 1, fenceEnd - firstNewLine - 1).Trim();
                    if (inside.StartsWith("{") && inside.EndsWith("}"))
                        return inside;
                }
            }
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);

        return null;
    }
}