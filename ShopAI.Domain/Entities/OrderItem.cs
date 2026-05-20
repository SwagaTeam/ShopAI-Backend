using Domain.Entities;
using Domain.Entities.Abstractions;

public class OrderItem : Entity
{
    protected OrderItem() { }

    public OrderItem(Guid productId, decimal priceAtPurchase, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive");

        ProductId = productId;
        PriceAtPurchase = priceAtPurchase;
        Quantity = quantity;
    }

    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }
    public decimal TotalPrice => PriceAtPurchase * Quantity;
    public virtual Product Product { get; private set; }
}