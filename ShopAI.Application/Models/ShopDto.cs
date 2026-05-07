namespace ShopAI.Application.Models;

public record ShopDto(
    Guid Id,
    string Name,
    string UrlAlias,
    Guid OwnerId,
    string? OwnerName
);

