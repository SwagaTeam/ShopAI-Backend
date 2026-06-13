using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations
{
    public class RecentlyViewedRepository(AppDbContext context) : Repository<RecentlyViewedProduct>(context), IRecentlyViewedRepository
    {
        private const int MaxHistorySize = 20; // Храним максимум 20 последних товаров

        public async Task TrackViewAsync(Guid userId, Guid productId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var id = Guid.CreateVersion7();

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""RecentlyViewedProduct"" (""Id"", ""UserId"", ""ProductId"", ""ViewedAtUtc"")
                VALUES ({id}, {userId}, {productId}, {now})
                ON CONFLICT (""UserId"", ""ProductId"")
                DO UPDATE SET ""ViewedAtUtc"" = EXCLUDED.""ViewedAtUtc"";", ct);

            // Очищаем старые записи, если превышен лимит
            var excessRecords = await context.Set<RecentlyViewedProduct>()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.ViewedAtUtc)
                .Skip(MaxHistorySize) // Пропускаем 20 самых свежих
                .ToListAsync(ct);

            if (excessRecords.Any())
            {
                context.Set<RecentlyViewedProduct>().RemoveRange(excessRecords);
                await context.SaveChangesAsync(ct);
            }
        }

        public async Task<List<Product>> GetHistoryAsync(Guid userId, int limit, CancellationToken ct)
        {
            var latestProductIds = await context.Set<RecentlyViewedProduct>()
                .Where(r => r.UserId == userId)
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ViewedAtUtc = g.Max(r => r.ViewedAtUtc)
                })
                .OrderByDescending(r => r.ViewedAtUtc)
                .Take(limit)
                .Select(r => r.ProductId)
                .ToListAsync(ct);

            var products = await context.Products
                .Where(p => latestProductIds.Contains(p.Id))
                .Include(p => p.Shop)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .ToListAsync(ct);

            return latestProductIds
                .Select(productId => products.FirstOrDefault(p => p.Id == productId))
                .Where(product => product != null)
                .Cast<Product>()
                .ToList();
        }
    }
}
