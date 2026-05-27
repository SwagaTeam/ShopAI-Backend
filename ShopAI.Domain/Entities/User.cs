using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class User : Entity
    {
        public const string AdminRole = "Admin";
        public const string UserRole = "User";

        public required string FullName { get; set; } = "unknown";
        public required string Email { get; set; } = "unknown";
        public required string Phone { get; set; } = "unknown";
        public required string Password { get; set; }
        public required string Salt { get; set; }
        public string Role { get; set; } = UserRole;

        public virtual ICollection<Shop> Shops { get; set; } = new List<Shop>();
        public virtual ICollection<FavoriteProduct> Favorites { get; set; } = new List<FavoriteProduct>();
        public virtual ICollection<RecentlyViewedProduct> RecentlyViewed { get; set; } = new List<RecentlyViewedProduct>();
        public virtual ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    }
}
