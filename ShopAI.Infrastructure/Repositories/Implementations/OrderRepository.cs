using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class OrderRepository(AppDbContext context) : Repository<Order>(context), IOrderRepository
{
    public async Task<List<Order>> GetByShopIdAsync(Guid shopId)
        => await _context.Orders
            .Include(o => o.Items) 
            .Where(o => o.ShopId == shopId)
            .ToListAsync();
}