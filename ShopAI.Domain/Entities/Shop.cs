using System.Runtime.CompilerServices;
using Domain.Entities.Abstractions;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Shop : Entity
{
    protected Shop() { }

    public Shop(string name, string urlAlias, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name required");

        Name = name;
        UrlAlias = urlAlias.ToLower();
        OwnerId = ownerId;
        Theme = ShopTheme.Default();
    }

    public string Name { get; set; }
    public string UrlAlias { get; set; }
    public Guid OwnerId { get; set; }

    public ShopTheme Theme { get; set; } = ShopTheme.Default();
    public User Owner { get; set; }
    
    public void UpdateTheme(string cssVariables, string configJson)
    {
        Theme = new ShopTheme(cssVariables, configJson);
    }
    
    public virtual ICollection<Product> Products { get; private set; } = new List<Product>();
    public virtual ICollection<Category> Categories { get; private set; } = new List<Category>();
}

