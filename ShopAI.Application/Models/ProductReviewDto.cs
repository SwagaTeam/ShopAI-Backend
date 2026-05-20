namespace ShopAI.Application.Models
{
    public record ProductReviewDto(
    Guid Id,
    Guid UserId,
    string UserName,
    int Rating,
    string Comment,
    DateTime CreatedAtUtc);
}
