using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class CategoryRepository(AppDbContext context) : Repository<Category>(context), ICategoryRepository
{
    public async Task<List<Category>> GetByShopIdAsync(Guid shopId)
        => await _context.Categories
            .Include(c => c.GlobalCategory)
            .Where(c => c.ShopId == shopId)
            .ToListAsync();
    
    public async Task<ICollection<Category>> GetAllWithShopsAsync(CancellationToken ct = default)
    {
        return await _context.Categories
            .AsNoTracking() 
            .Include(c => c.Shop)
            .ToListAsync(ct);
    }
}
