using Domain.Entities.Abstractions;

namespace Domain.Entities
{
    public class RefreshToken : Entity
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
