namespace ShopAI.Application.Models
{
    public record PagedListDto<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
}
