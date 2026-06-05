using Domain.Entities.Abstractions;
using Domain.Enums;

namespace Domain.Entities;

public class SellerAccessRequest : Entity
{
    public Guid UserId { get; set; }
    public string InnOrOgrnip { get; set; } = string.Empty;
    public string SocialOrWebsiteUrl { get; set; } = string.Empty;
    public PlannedProductCategory PlannedCategory { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool AcceptedMarketplaceRules { get; set; }
    public SellerAccessRequestStatus Status { get; set; } = SellerAccessRequestStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public string? AdminComment { get; set; }

    public User? User { get; set; }
    public User? ReviewedByAdmin { get; set; }
}
