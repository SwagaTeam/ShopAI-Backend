namespace ShopAI.Application.Models;

public record ProductShortDto(
    Guid Id,
    string Name,
    decimal Price,
    string ImageUrl,
    string ShopName,    
    string? BrandName,  
    int StockQuantity
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
    string? BrandName
);