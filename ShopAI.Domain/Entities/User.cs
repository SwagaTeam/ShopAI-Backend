using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class User : Entity
    {
        public required string FullName { get; set; } = "unknown";
        public required string Email { get; set; } = "unknown";
        public required string Phone { get; set; } = "unknown";
        public required string Password { get; set; }
        public required string Salt { get; set; }

        public virtual ICollection<Shop> Shops { get; set; } = new List<Shop>();
    }
}
