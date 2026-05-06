using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class CategoryRepository(AppDbContext context) : Repository<Category>(context), ICategoryRepository
{
    public async Task<List<Category>> GetByShopIdAsync(Guid shopId)
        => await _context.Categories.Where(c => c.ShopId == shopId).ToListAsync();
}