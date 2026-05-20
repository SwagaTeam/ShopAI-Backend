namespace ShopAI.Application.Models;

public record ShopDto(
    Guid Id,
    string Name,
    string Description,
    string LogoPath,
    string UrlAlias,
    Guid OwnerId,
    string? OwnerName
);

