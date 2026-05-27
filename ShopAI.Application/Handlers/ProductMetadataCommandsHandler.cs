using System.Text.Json;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record SetProductTagsCommand(Guid ProductId, List<string> Tags) : IRequest;
public record AddProductTagsCommand(Guid ProductId, List<string> Tags) : IRequest;
public record RemoveProductTagsCommand(Guid ProductId, List<string> Tags) : IRequest;
public record SetProductAttributesCommand(Guid ProductId, Dictionary<string, string> Attributes) : IRequest;
public record RemoveProductAttributeCommand(Guid ProductId, string AttributeKey) : IRequest;

public class ProductMetadataCommandsHandler(
    IProductRepository productRepository)
    : IRequestHandler<SetProductTagsCommand>,
      IRequestHandler<AddProductTagsCommand>,
      IRequestHandler<RemoveProductTagsCommand>,
      IRequestHandler<SetProductAttributesCommand>,
      IRequestHandler<RemoveProductAttributeCommand>
{
    public async Task Handle(SetProductTagsCommand request, CancellationToken cancellationToken)
    {
        var product = await GetProduct(request.ProductId);
        var tags = NormalizeTags(request.Tags);
        product.Tags = string.Join(",", tags);
        productRepository.Update(product);
        await productRepository.SaveAsync(cancellationToken);
    }

    public async Task Handle(AddProductTagsCommand request, CancellationToken cancellationToken)
    {
        var product = await GetProduct(request.ProductId);
        var current = ParseTags(product.Tags);
        var updated = current.Union(NormalizeTags(request.Tags)).ToList();
        product.Tags = string.Join(",", updated);
        productRepository.Update(product);
        await productRepository.SaveAsync(cancellationToken);
    }

    public async Task Handle(RemoveProductTagsCommand request, CancellationToken cancellationToken)
    {
        var product = await GetProduct(request.ProductId);
        var toRemove = NormalizeTags(request.Tags).ToHashSet();
        var updated = ParseTags(product.Tags).Where(t => !toRemove.Contains(t)).ToList();
        product.Tags = string.Join(",", updated);
        productRepository.Update(product);
        await productRepository.SaveAsync(cancellationToken);
    }

    public async Task Handle(SetProductAttributesCommand request, CancellationToken cancellationToken)
    {
        var product = await GetProduct(request.ProductId);
        product.AttributesJson = JsonSerializer.Serialize(request.Attributes ?? new Dictionary<string, string>());
        productRepository.Update(product);
        await productRepository.SaveAsync(cancellationToken);
    }

    public async Task Handle(RemoveProductAttributeCommand request, CancellationToken cancellationToken)
    {
        var product = await GetProduct(request.ProductId);
        var attrs = ParseAttributes(product.AttributesJson);
        attrs.Remove(request.AttributeKey);
        product.AttributesJson = JsonSerializer.Serialize(attrs);
        productRepository.Update(product);
        await productRepository.SaveAsync(cancellationToken);
    }

    private async Task<Domain.Entities.Product> GetProduct(Guid productId)
    {
        var product = await productRepository.GetByIdAsync(productId);
        return product ?? throw new KeyNotFoundException("Товар не найден.");
    }

    private static List<string> ParseTags(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();

    private static List<string> NormalizeTags(List<string> tags) =>
        (tags ?? [])
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Select(t => t.Trim().ToLowerInvariant())
        .Distinct()
        .ToList();

    private static Dictionary<string, string> ParseAttributes(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
