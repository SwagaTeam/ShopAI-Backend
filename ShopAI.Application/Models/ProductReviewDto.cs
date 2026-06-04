namespace ShopAI.Application.Models
{
    public class ProductReviewDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public List<string>? ImagePaths { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
    }
}