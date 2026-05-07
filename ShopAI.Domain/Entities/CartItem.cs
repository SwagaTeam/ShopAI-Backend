using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class CartItem : Entity
    {
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }

        public virtual Cart Cart { get; set; }
        public virtual Product Product { get; set; }
    }
}
