using Domain.Entities.Abstractions;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Shop(string name, string urlAlias, Guid ownerId) : Entity
{
    public string Name { get; private set; } = name;
    public string UrlAlias { get; private set; } = urlAlias.ToLower(); 
    public Guid OwnerId { get; private set; } = ownerId;

    public ShopTheme Theme { get; private set; } = ShopTheme.Default();
    
    public void UpdateTheme(string cssVariables, string configJson)
    {
        Theme = new ShopTheme(cssVariables, configJson);
    }
    
    public virtual ICollection<Product> Products { get; private set; } = new List<Product>();
    public virtual ICollection<Category> Categories { get; private set; } = new List<Category>();
}

