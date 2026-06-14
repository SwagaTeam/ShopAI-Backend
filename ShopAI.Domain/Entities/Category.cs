using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class Category : Entity
{
    protected Category() { }

    public Category(string name, Guid shopId, Guid? parentCategoryId = null, Guid? globalCategoryId = null)
    {
        Name = name;
        ShopId = shopId;
        ParentCategoryId = parentCategoryId;
        GlobalCategoryId = globalCategoryId;
    }

    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public Guid ShopId { get; set; }
    public Guid? GlobalCategoryId { get; set; }
    
    public virtual Shop Shop { get; set; }
    public virtual GlobalCategory? GlobalCategory { get; set; }
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
