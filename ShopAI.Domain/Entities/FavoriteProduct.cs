using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class FavoriteProduct : Entity
    {
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }
        public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
        public virtual User User { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}
