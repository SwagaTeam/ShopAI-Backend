namespace ShopAI.Application.Models;

public record ProductShortDto(
    Guid Id,
    string Name,
    decimal Price,
    string ImageUrl,
    string ShopName,    
    string? BrandName,  
    int StockQuantity,
    decimal Rating = 0,
    int ReviewsCount = 0,
    bool IsInWishlist = false,
    int CartQuantity = 0,
    List<string>? Tags = null,
    Dictionary<string, string>? Attributes = null,
    List<string>? ImageUrls = null
);

public record ProductDetailsDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    int StockQuantity,
    Guid CategoryId,
    string CategoryName,
    Guid ShopId,
    string ShopName,
    string? BrandName,
    decimal Rating = 0,
    int ReviewsCount = 0,
    bool IsInWishlist = false,
    int CartQuantity = 0,
    List<string>? Tags = null,
    Dictionary<string, string>? Attributes = null,
    List<string>? ImageUrls = null
);
