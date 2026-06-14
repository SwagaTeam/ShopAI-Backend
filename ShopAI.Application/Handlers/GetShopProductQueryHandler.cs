using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;

namespace ShopAI.Application.Handlers;

public record GetShopProductsQuery(Guid ShopId, int Page = 1, int PageSize = 10)
    : IRequest<PagedListDto<ProductShortDto>>;

public class GetShopProductsQueryHandler(
    AppDbContext context,
    IProductDtoFactory productDtoFactory)
    : IRequestHandler<GetShopProductsQuery, PagedListDto<ProductShortDto>>
{
    public async Task<PagedListDto<ProductShortDto>> Handle(GetShopProductsQuery request, CancellationToken ct)
    {
        var shopExists = await context.Shops.AnyAsync(s => s.Id == request.ShopId, ct);
        if (!shopExists)
            throw new KeyNotFoundException("Shop was not found.");

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.ShopId == request.ShopId && p.StockQuantity > 0);

        var totalCount = await query.CountAsync(ct);

        var products = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = await productDtoFactory.CreateShortDtosAsync(products, ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedListDto<ProductShortDto>(items, page, pageSize, totalCount, totalPages);
    }
}
