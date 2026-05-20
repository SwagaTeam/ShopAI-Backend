namespace ShopAI.Application.Models
{
    public record ProductReviewDto(
    Guid Id,
    Guid UserId,
    string UserName,
    List<string> ImagePaths,
    int Rating,
    string Comment,
    DateTime CreatedAtUtc);
}
