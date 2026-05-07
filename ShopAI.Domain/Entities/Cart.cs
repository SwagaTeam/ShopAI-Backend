using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class Cart : Entity
    {
        public Guid UserId { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public virtual User User { get; set; }

        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}