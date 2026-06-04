using System.Security.Claims;
using System.Text.Json;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Helpers.Implementations;

public class ProductDtoFactory(
    AppDbContext context,
    IFileStorageService fileStorageService,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor) : IProductDtoFactory
{
    public async Task<List<ProductShortDto>> CreateShortDtosAsync(
        IReadOnlyCollection<Product> products,
        CancellationToken ct = default)
    {
        if (products.Count == 0) return [];

        var productIds = products.Select(p => p.Id).Distinct().ToList();
        var shopIds = products.Select(p => p.ShopId).Distinct().ToList();
        var brandIds = products.Where(p => p.BrandId.HasValue).Select(p => p.BrandId!.Value).Distinct().ToList();

        var shops = await context.Shops
            .AsNoTracking()
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var brands = await context.Brands
            .AsNoTracking()
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var ratingStats = await LoadRatingStatsAsync(productIds, ct);
        var wishlist = await LoadWishlistAsync(productIds, ct);
        var cart = await LoadCartAsync(productIds, ct);
        var imageUrls = await LoadImageUrlsAsync(productIds, ct);

        var result = new List<ProductShortDto>(products.Count);
        foreach (var product in products)
        {
            ratingStats.TryGetValue(product.Id, out var rating);
            cart.TryGetValue(product.Id, out var quantity);
            imageUrls.TryGetValue(product.Id, out var gallery);
            var mainImageUrl = await ResolveUrlAsync(product.ImageUrl);
            var allImageUrls = BuildImageList(mainImageUrl, gallery);

            result.Add(new ProductShortDto(
                product.Id,
                product.Name,
                product.Price,
                mainImageUrl,
                product.Shop?.Name ?? shops.GetValueOrDefault(product.ShopId) ?? string.Empty,
                product.Brand?.Name ?? (product.BrandId.HasValue ? brands.GetValueOrDefault(product.BrandId.Value) : null),
                product.StockQuantity,
                rating.Rating,
                rating.ReviewsCount,
                wishlist.Contains(product.Id),
                quantity,
                ParseTags(product.Tags),
                ParseAttributes(product.AttributesJson),
                allImageUrls));
        }

        return result;
    }

    public async Task<ProductDetailsDto> CreateDetailsDtoAsync(Product product, CancellationToken ct = default)
    {
        var shortDto = (await CreateShortDtosAsync([product], ct)).Single();
        var categoryName = product.Category?.Name
                           ?? await context.Categories
                               .AsNoTracking()
                               .Where(c => c.Id == product.CategoryId)
                               .Select(c => c.Name)
                               .SingleOrDefaultAsync(ct)
                           ?? string.Empty;

        return new ProductDetailsDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            shortDto.ImageUrl,
            product.StockQuantity,
            product.CategoryId,
            categoryName,
            product.ShopId,
            shortDto.ShopName,
            shortDto.BrandName,
            shortDto.Rating,
            shortDto.ReviewsCount,
            shortDto.IsInWishlist,
            shortDto.CartQuantity,
            shortDto.Tags,
            shortDto.Attributes,
            shortDto.ImageUrls);
    }

    private async Task<Dictionary<Guid, (decimal Rating, int ReviewsCount)>> LoadRatingStatsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        return await context.ProductReviews
            .AsNoTracking()
            .Where(r => productIds.Contains(r.ProductId))
            .GroupBy(r => r.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Rating = Math.Round((decimal)g.Average(r => r.Rating), 1),
                ReviewsCount = g.Count()
            })
            .ToDictionaryAsync(x => x.ProductId, x => (x.Rating, x.ReviewsCount), ct);
    }

    private async Task<HashSet<Guid>> LoadWishlistAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue) return [];

        var ids = await context.Set<FavoriteProduct>()
            .AsNoTracking()
            .Where(f => f.UserId == userId.Value && productIds.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    private async Task<Dictionary<Guid, int>> LoadCartAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue) return [];

        return await context.Set<CartItem>()
            .AsNoTracking()
            .Where(ci => ci.Cart.UserId == userId.Value && productIds.Contains(ci.ProductId))
            .GroupBy(ci => ci.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(ci => ci.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);
    }

    private async Task<Dictionary<Guid, List<string>>> LoadImageUrlsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        var files = await context.FileMetadatas
            .AsNoTracking()
            .Where(f => f.ProductId.HasValue && productIds.Contains(f.ProductId.Value))
            .OrderBy(f => f.CreatedAt)
            .Select(f => new { ProductId = f.ProductId!.Value, f.Bucket, f.ObjectName })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, List<string>>();
        foreach (var file in files)
        {
            if (!result.TryGetValue(file.ProductId, out var urls))
            {
                urls = [];
                result[file.ProductId] = urls;
            }

            urls.Add(await fileStorageService.GetPresignedUrlAsync(file.Bucket, file.ObjectName));
        }

        return result;
    }

    private Guid? TryGetUserId()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier) ?? user?.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var userId) ? userId : null;
    }

    private async Task<string> ResolveUrlAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;

        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        return await fileStorageService.GetPresignedUrlAsync(bucket, value);
    }

    private static List<string> BuildImageList(string mainImageUrl, List<string>? gallery)
    {
        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(mainImageUrl)) urls.Add(mainImageUrl);
        if (gallery != null) urls.AddRange(gallery);

        return urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseTags(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
