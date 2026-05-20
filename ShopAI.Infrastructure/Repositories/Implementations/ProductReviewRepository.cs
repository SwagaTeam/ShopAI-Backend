using System;
using System.Collections.Generic;
using System.Text;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations
{
    public class ProductReviewRepository(AppDbContext context) : Repository<ProductReview>(context), IProductReviewRepository
    {
        public async Task<bool> HasUserReviewedProductAsync(Guid userId, Guid productId, CancellationToken ct)
        {
            return await context.Set<ProductReview>()
                .AnyAsync(r => r.UserId == userId && r.ProductId == productId, ct);
        }

        public async Task<List<ProductReview>> GetByProductIdAsync(Guid productId, int skip, int take, CancellationToken ct)
        {
            return await context.Set<ProductReview>()
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }
    }
    }
