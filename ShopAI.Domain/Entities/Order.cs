using Domain.Entities;
using Domain.Entities.Abstractions;
using Domain.Enums;
using Domain.ValueObjects;

public class Order : Entity
{
    protected Order() { }

    public Order(Guid shopId, User user)
    {
        ShopId = shopId;
        User = user;
        CreatedAt = DateTime.UtcNow;
        Status = OrderStatus.New;
    }

    public Guid ShopId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; private set; }
    public OrderStatus Status { get; set; }
    public string? PaymentProvider { get; set; }
    public string? PaymentProviderId { get; set; }
    public string? PaymentStatus { get; set; }
    public string? PaymentConfirmationUrl { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public Guid? DeliveryAddressId { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public virtual User User { get; set; }

    public virtual Shop Shop { get; private set; }
    public virtual DeliveryAddress? DeliveryAddressRef { get; set; }
    public virtual ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();
}
