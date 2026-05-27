using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class FileMetadata : Entity
{
    public string Bucket { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? ProductId { get; set; }
    public Guid? ShopId { get; set; }
    public Product? Product { get; set; }
    public Shop? Shop { get; set; }
}
