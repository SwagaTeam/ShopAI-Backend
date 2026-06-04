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
            // 1. Проверяем, смотрел ли юзер этот товар ранее
            var historyRecord = await context.Set<RecentlyViewedProduct>()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId, ct);

            if (historyRecord != null)
            {
                // Если смотрел — просто обновляем время, чтобы товар поднялся наверх
                historyRecord.ViewedAtUtc = DateTime.UtcNow;
            }
            else
            {
                // Если нет — добавляем новую запись
                historyRecord = new RecentlyViewedProduct
                {
                    UserId = userId,
                    ProductId = productId,
                    ViewedAtUtc = DateTime.UtcNow
                };
                context.Set<RecentlyViewedProduct>().Add(historyRecord);
            }

            await context.SaveChangesAsync(ct);

            // 2. Очищаем старые записи, если превышен лимит
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
            return await context.Set<RecentlyViewedProduct>()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.ViewedAtUtc)
                .Take(limit)
                .Select(r => r.Product)
                .Include(p => p.Shop)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .ToListAsync(ct);
        }
    }
}
