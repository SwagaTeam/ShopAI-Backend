using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;

namespace ShopAI.Application.Handlers;

public record GetShopProductsQuery(Guid ShopId, int Page = 1, int PageSize = 10)
    : IRequest<PagedListDto<ProductShortDto>>;

public class GetShopProductsQueryHandler(
    AppDbContext context, // Можно использовать IProductRepository, если он есть
    AutoMapper.IMapper mapper)
    : IRequestHandler<GetShopProductsQuery, PagedListDto<ProductShortDto>>
{
    public async Task<PagedListDto<ProductShortDto>> Handle(GetShopProductsQuery request, CancellationToken ct)
    {
        // 1. Проверяем, существует ли вообще такой магазин
        var shopExists = await context.Shops.AnyAsync(s => s.Id == request.ShopId, ct);
        if (!shopExists)
            throw new KeyNotFoundException("Магазин не найден.");

        // 2. Строим базовый запрос к товарам этого магазина
        var query = context.Products
            .AsNoTracking()
            .Include(p => p.Shop) // Нужно для маппинга ShopName в ProductShortDto
            .Include(p => p.Brand) // Нужно для маппинга BrandName в ProductShortDto
            .Where(p => p.ShopId == request.ShopId);

        // 3. Считаем общее количество товаров
        var totalCount = await query.CountAsync(ct);

        // 4. Забираем нужную страницу
        var products = await query
            .OrderByDescending(p => p.Id) // Сначала новые, или поменяй сортировку на Name
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // 5. Маппим сущности в DTO
        var dtos = mapper.Map<List<ProductShortDto>>(products);

        // 6. Считаем общее количество страниц
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedListDto<ProductShortDto>(dtos, request.Page, request.PageSize, totalCount, totalPages);
    }
}