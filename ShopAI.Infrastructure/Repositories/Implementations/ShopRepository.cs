using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class ShopRepository(AppDbContext context) : Repository<Shop>(context), IShopRepository
{
    public override async Task<Shop?> GetByIdAsync(Guid id)
    {
        return await _context.Shops
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

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

    public async Task<List<Shop>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _context.Set<Shop>()
            .AsNoTracking() // Отключаем трекинг для быстрого чтения
            .Include(s => s.Owner) // Инклудим Owner, чтобы маппер смог заполнить поле OwnerName в ShopDto
            .Where(s => s.OwnerId == ownerId)
            .ToListAsync(ct);
    }
}