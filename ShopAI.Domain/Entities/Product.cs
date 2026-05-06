using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class Product(
    Guid shopId,
    string name, 
    decimal price, 
    Guid categoryId,
    string description,
    string imageUrl, 
    int stockQuantity, 
    Guid? brandIdq)
    : Entity
{
    public Guid ShopId { get; private set; } = shopId;
    public string Name { get; private set; } = name;
    public string Description { get; private set; } = description;
    public decimal Price { get; private set; } = price;
    public string ImageUrl { get; private set; } = imageUrl;
    public int StockQuantity { get; private set; } = stockQuantity;
    
    public Guid CategoryId { get; private set; } = categoryId;
    public Guid? BrandId { get; private set; } = brandIdq;
    
    public virtual Shop Shop { get; private set; }
    public virtual Category Category { get; private set; }
    public virtual Brand? Brand { get; private set; }
}