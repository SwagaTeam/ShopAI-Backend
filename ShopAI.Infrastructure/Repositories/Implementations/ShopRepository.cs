using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class ShopRepository(AppDbContext context) : Repository<Shop>(context), IShopRepository
{
    public async Task<Shop?> GetByUrlAliasAsync(string alias) 
        => await _context.Shops.FirstOrDefaultAsync(s => s.UrlAlias == alias.ToLower());

    public async Task<List<Shop>> GetShopsByCategoryAsync(Guid categoryId)
    {
        return await _context.Products
            .Where(p => p.CategoryId == categoryId)
            .Select(p => _context.Shops.First(s => s.Id == p.ShopId))
            .Distinct()
            .ToListAsync();
    }
}