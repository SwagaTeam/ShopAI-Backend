using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Handlers;

public record GetShopProductsQuery(Guid ShopId, int Page = 1, int PageSize = 10)
    : IRequest<PagedListDto<ProductShortDto>>;

public class GetShopProductsQueryHandler(
    AppDbContext context,
    IFileStorageService fileStorageService,
    IConfiguration configuration,
    IMapper mapper)
    : IRequestHandler<GetShopProductsQuery, PagedListDto<ProductShortDto>>
{
    public async Task<PagedListDto<ProductShortDto>> Handle(GetShopProductsQuery request, CancellationToken ct)
    {
        var shopExists = await context.Shops.AnyAsync(s => s.Id == request.ShopId, ct);
        if (!shopExists)
            throw new KeyNotFoundException("Магазин не найден.");

        var query = context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Where(p => p.ShopId == request.ShopId);

        var totalCount = await query.CountAsync(ct);

        var products = await query
            .OrderByDescending(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = mapper.Map<List<ProductShortDto>>(products);
        var withUrls = new List<ProductShortDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            withUrls.Add(dto with { ImageUrl = await ResolveUrlAsync(dto.ImageUrl) });
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        return new PagedListDto<ProductShortDto>(withUrls, request.Page, request.PageSize, totalCount, totalPages);
    }

    private async Task<string> ResolveUrlAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;
        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        return await fileStorageService.GetPresignedUrlAsync(bucket, value);
    }
}
