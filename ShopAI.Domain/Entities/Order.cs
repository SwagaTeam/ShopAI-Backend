using Domain.Entities.Abstractions;
using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Order(Guid shopId, CustomerInfo customer) : Entity
{
    public Guid ShopId { get; private set; } = shopId;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public OrderStatus Status { get; private set; } = OrderStatus.New;
    public CustomerInfo Customer { get; private set; } = customer;
    
    public virtual Shop Shop { get; private set; }
    public virtual ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();
}


