using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class OrderItem : Entity
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal PriceAtPurchase { get; private set; } 

    public decimal TotalPrice => PriceAtPurchase * Quantity;

    public OrderItem(Guid productId, decimal price, int quantity, Product product)
    {
        if (quantity <= 0) 
            throw new ArgumentException("Quantity must be positive");

        ProductId = productId;
        PriceAtPurchase = price;
        Quantity = quantity;
        Product = product;
    }
    
    public virtual Product Product { get; private set; }
}