using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class DeliveryAddress : Entity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? Entrance { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual User? User { get; set; }
}
