using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class Category(string name, Guid shopId, Guid? parentId = null) : Entity
{
    public string Name { get; private set; } = name;
    public Guid? ParentCategoryId { get; private set; } = parentId;
    public Guid ShopId { get; private set; } = shopId;
    
    public virtual Shop Shop { get; private set; }
    public virtual ICollection<Product> Products { get; private set; } = new List<Product>();
}