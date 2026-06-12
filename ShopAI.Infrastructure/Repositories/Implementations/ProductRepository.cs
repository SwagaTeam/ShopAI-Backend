using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Requests;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class ProductRepository(AppDbContext context) : Repository<Product>(context), IProductRepository
{
    public async Task<Product?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id) ?? null;
    }

    public async Task<List<Product>> GetByShopIdAsync(Guid shopId)
        => await _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.ShopId == shopId)
            .ToListAsync();

    public async Task<List<Product>> GetLatestAsync(int count)
        => await _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .OrderByDescending(p => p.Id)
            .Take(count)
            .ToListAsync();

    public async Task<List<Product>> GetPopularAsync(int count)
        => await _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
            .ThenByDescending(p => p.Reviews.Count)
            .ThenByDescending(p => p.StockQuantity)
            .Take(count)
            .ToListAsync();

    public async Task<List<Product>> GetProductsByShopAndCategoryAsync(Guid shopId, Guid categoryId)
        => await _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.ShopId == shopId && p.CategoryId == categoryId)
            .ToListAsync();
    
    public async Task<List<Product>> GetProductsByCategoryAsync(Guid categoryId)
        => await _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId)
            .ToListAsync();

    public async Task<(List<Product> Items, int TotalCount)> GetByFiltersAsync(
        GetProductsByFiltersRequest filters, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .Include(p => p.Shop)
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Reviews)
            .AsQueryable();

        query = ApplyFilters(query, filters);
        
        query = ApplySorting(query, filters);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((filters.PageNumber - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private IQueryable<Product> ApplyFilters(IQueryable<Product> query, GetProductsByFiltersRequest filters)
    {
        if (filters.ShopId.HasValue)
            query = query.Where(p => p.ShopId == filters.ShopId.Value);

        if (filters.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filters.CategoryId.Value);

        if (filters.BrandId.HasValue)
            query = query.Where(p => p.BrandId == filters.BrandId.Value);

        if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
        {
            var searchTerm = filters.SearchTerm.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                p.Description.ToLower().Contains(searchTerm) ||
                p.Tags.ToLower().Contains(searchTerm));
        }

        if (filters.MinPrice.HasValue)
            query = query.Where(p => p.Price >= filters.MinPrice.Value);

        if (filters.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= filters.MaxPrice.Value);

        if (filters.MinStock.HasValue)
            query = query.Where(p => p.StockQuantity >= filters.MinStock.Value);

        if (filters.MaxStock.HasValue)
            query = query.Where(p => p.StockQuantity <= filters.MaxStock.Value);

        if (!string.IsNullOrWhiteSpace(filters.Tags))
        {
            var tags = filters.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(p => tags.Any(t => p.Tags.Contains(t)));
        }

        if (filters.InStock.HasValue && filters.InStock.Value)
            query = query.Where(p => p.StockQuantity > 0);

        if (filters.MinRating.HasValue)
        {
            query = query.Where(p => p.Reviews.Any() &&
                p.Reviews.Average(r => r.Rating) >= filters.MinRating.Value);
        }

        return query;
    }

    private IQueryable<Product> ApplySorting(IQueryable<Product> query, GetProductsByFiltersRequest filters)
    {
        if (string.IsNullOrWhiteSpace(filters.SortBy))
        {
            return query.OrderByDescending(p => p.Id);
        }
    
        return filters.SortBy.ToLower() switch
        {
            "price" => filters.SortDescending
                ? query.OrderByDescending(p => p.Price)
                : query.OrderBy(p => p.Price),
            "name" => filters.SortDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
            "rating" => filters.SortDescending
                ? query.OrderByDescending(p => p.Reviews.Average(r => r.Rating))
                : query.OrderBy(p => p.Reviews.Average(r => r.Rating)),
            "stock" => filters.SortDescending
                ? query.OrderByDescending(p => p.StockQuantity)
                : query.OrderBy(p => p.StockQuantity),
            _ => query.OrderByDescending(p => p.Id) 
        };
    }
}
