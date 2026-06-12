namespace ShopAI.Infrastructure.Requests;

public class GetProductsByFiltersRequest
{
    public Guid? ShopId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public string? SearchTerm { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinStock { get; set; }
    public int? MaxStock { get; set; }
    public string? Tags { get; set; }
    public bool? InStock { get; set; }
    public double? MinRating { get; set; }
    public string? SortBy { get; set; } // "price", "name", "rating", "createdAt"
    public bool SortDescending { get; set; } = false;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}