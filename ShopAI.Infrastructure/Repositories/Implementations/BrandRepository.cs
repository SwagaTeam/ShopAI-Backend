using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class BrandRepository(AppDbContext context) : Repository<Brand>(context), IBrandRepository
{
    public async Task<Brand?> GetByNameAsync(string name)
        => await _context.Brands.FirstOrDefaultAsync(b => b.Name == name);
}