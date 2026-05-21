namespace ShopAI.Application.Models;

public record ShopDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LogoPath { get; init; } = string.Empty;
    public string UrlAlias { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public string? OwnerName { get; init; }
}

