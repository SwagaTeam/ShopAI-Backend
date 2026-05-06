using Domain.Entities;
using Domain.Entities.Abstractions;
using Domain.Enums;
using Domain.ValueObjects;

public class Order : Entity
{
    protected Order() { }

    public Order(Guid shopId, CustomerInfo customer)
    {
        ShopId = shopId;
        Customer = customer;
        CreatedAt = DateTime.UtcNow;
        Status = OrderStatus.New;
    }

    public Guid ShopId { get; set; }
    public DateTime CreatedAt { get; private set; }
    public OrderStatus Status { get; set; }
    public CustomerInfo Customer { get; set; }

    public virtual Shop Shop { get; private set; }
    public virtual ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();
}