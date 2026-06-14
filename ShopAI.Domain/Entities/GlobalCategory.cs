using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class GlobalCategory : Entity
{
    protected GlobalCategory() { }

    public GlobalCategory(string name, string slug, int sortOrder = 0)
    {
        Name = name;
        Slug = slug;
        SortOrder = sortOrder;
    }

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Category> StoreCategories { get; set; } = new List<Category>();
}
