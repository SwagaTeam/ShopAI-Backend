using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Repositories.Implementations;

public class RefreshTokenRepository(AppDbContext context)
    : Repository<RefreshToken>(context), IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await _context.Set<RefreshToken>()
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == token, ct);
    }
}