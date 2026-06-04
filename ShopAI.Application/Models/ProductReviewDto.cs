namespace ShopAI.Application.Models
{
    public record ProductReviewDto(
        Guid Id = default,
        Guid UserId = default,
        string UserName = default,
        List<string>? ImagePaths = null,
        int Rating = 0,
        string Comment = "",
        DateTime CreatedAtUtc = default);
}