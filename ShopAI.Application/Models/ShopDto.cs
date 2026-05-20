namespace ShopAI.Application.Models;

public record ShopDto(
    Guid Id,
    string Name,
    string Description,
    string UrlAlias,
    Guid OwnerId,
    string? OwnerName
);

