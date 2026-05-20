using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class Product : Entity
{
    protected Product() { }

    public Product(
        Guid shopId,
        string name,
        decimal price,
        Guid categoryId,
        string description,
        string imageUrl,
        int stockQuantity,
        Guid? brandId)
        : base()
    {
        ShopId = shopId;
        Name = name;
        Description = description;
        Price = price;
        ImageUrl = imageUrl;
        StockQuantity = stockQuantity;
        CategoryId = categoryId;
        BrandId = brandId;
    }

    public Guid ShopId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string ImageUrl { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }

    public virtual Shop Shop { get; set; }
    public virtual Category Category { get; set; }
    public virtual Brand? Brand { get; set; }

    public virtual ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}