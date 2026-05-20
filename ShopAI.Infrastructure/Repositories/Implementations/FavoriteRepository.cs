using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class FavoriteRepository(AppDbContext context) : Repository<FavoriteProduct>(context), IFavoriteRepository
{
    public async Task<FavoriteProduct?> GetFavoriteAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        return await context.Set<FavoriteProduct>()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);
    }

    public async Task<List<Product>> GetUserFavoritesAsync(Guid userId, CancellationToken ct)
    {
        return await context.Set<FavoriteProduct>()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.AddedAtUtc)
            .Select(f => f.Product)
            .ToListAsync(ct);
    }
}