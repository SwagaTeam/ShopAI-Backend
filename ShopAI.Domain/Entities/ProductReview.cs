using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class ProductReview : Entity
    {
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public List<string> ImagePaths { get; set; } = new();
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}
