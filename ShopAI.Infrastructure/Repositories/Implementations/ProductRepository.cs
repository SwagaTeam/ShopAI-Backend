using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

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
}
