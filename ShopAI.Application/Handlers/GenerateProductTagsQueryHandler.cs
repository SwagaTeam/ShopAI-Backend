using System.Text;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;

namespace ShopAI.Application.Handlers;

public record GenerateProductTagsQuery(GenerateProductTagsRequest Request) : IRequest<GenerateProductTagsResponse>;

public class GenerateProductTagsQueryHandler(IOpenRouterClient openRouterClient)
    : IRequestHandler<GenerateProductTagsQuery, GenerateProductTagsResponse>
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "для", "это", "или", "and", "the", "with", "demo", "товар", "product", "shopai"
    };

    public async Task<GenerateProductTagsResponse> Handle(GenerateProductTagsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Request.Limit ?? 12, 1, 30);
        var aiTags = await openRouterClient.GenerateProductTagsAsync(
            request.Request.Name,
            request.Request.Description,
            request.Request.Attributes,
            limit,
            ct);

        var tags = Normalize(aiTags).Take(limit).ToList();
        if (tags.Count == 0)
            tags = BuildFallback(request.Request).Take(limit).ToList();

        return new GenerateProductTagsResponse(tags);
    }

    private static List<string> BuildFallback(GenerateProductTagsRequest request)
    {
        var raw = new List<string>();
        raw.AddRange(Tokenize(request.Name));
        raw.AddRange(Tokenize(request.Description));

        if (request.Attributes != null)
        {
            foreach (var attr in request.Attributes)
            {
                raw.AddRange(Tokenize(attr.Key));
                raw.AddRange(Tokenize(attr.Value));
            }
        }

        var normalized = Normalize(raw).ToList();
        var expanded = new List<string>(normalized);

        if (normalized.Any(t => t.Contains("phone") || t.Contains("смартфон")))
            expanded.AddRange(["smartphone", "телефон", "android", "mobile"]);
        if (normalized.Any(t => t.Contains("headphone") || t.Contains("earbuds") || t.Contains("наушники")))
            expanded.AddRange(["audio", "bluetooth", "наушники"]);
        if (normalized.Any(t => t.Contains("coffee") || t.Contains("кофе")))
            expanded.AddRange(["coffee", "кофе", "home"]);
        if (normalized.Any(t => t.Contains("sneaker") || t.Contains("кроссов")))
            expanded.AddRange(["sneakers", "обувь", "streetwear"]);
        if (normalized.Any(t => t.Contains("fitness") || t.Contains("sport") || t.Contains("спорт")))
            expanded.AddRange(["fitness", "sport", "training"]);

        return Normalize(expanded).ToList();
    }

    private static IEnumerable<string> Normalize(IEnumerable<string?>? tags)
    {
        return (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim().ToLowerInvariant())
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-')
            {
                current.Append(char.ToLowerInvariant(ch));
                continue;
            }

            Flush();
        }

        Flush();
        return result;

        void Flush()
        {
            if (current.Length > 1)
            {
                result.Add(current.ToString());
            }

            current.Clear();
        }
    }
}
