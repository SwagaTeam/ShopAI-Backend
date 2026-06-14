using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GlobalCategoriesController(
    AppDbContext context,
    IProductDtoFactory productDtoFactory) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<GlobalCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GlobalCategoryDto>>> GetAll(CancellationToken ct)
    {
        var categories = await context.GlobalCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new GlobalCategoryDto(c.Id, c.Name, c.Slug, c.SortOrder))
            .ToListAsync(ct);

        return Ok(categories);
    }

    [HttpGet("{id:guid}/products")]
    [ProducesResponseType(typeof(List<ProductShortDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductShortDto>>> GetProductsById(
        Guid id,
        [FromQuery] int limit = 40,
        CancellationToken ct = default)
    {
        return Ok(await LoadProductsAsync(id, limit, ct));
    }

    [HttpGet("slug/{slug}/products")]
    [ProducesResponseType(typeof(List<ProductShortDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductShortDto>>> GetProductsBySlug(
        string slug,
        [FromQuery] int limit = 40,
        CancellationToken ct = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var categoryId = await context.GlobalCategories
            .AsNoTracking()
            .Where(c => c.Slug == normalizedSlug && c.IsActive)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(ct);

        return Ok(categoryId.HasValue
            ? await LoadProductsAsync(categoryId.Value, limit, ct)
            : []);
    }

    private async Task<List<ProductShortDto>> LoadProductsAsync(
        Guid globalCategoryId,
        int limit,
        CancellationToken ct)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var products = await context.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .ThenInclude(c => c.GlobalCategory)
            .Include(p => p.Reviews)
            .Where(p => p.StockQuantity > 0)
            .Where(p => p.Category.GlobalCategoryId == globalCategoryId)
            .Where(p => p.Category.GlobalCategory != null && p.Category.GlobalCategory.IsActive)
            .OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
            .ThenByDescending(p => p.Reviews.Count)
            .ThenByDescending(p => p.Id)
            .Take(safeLimit)
            .ToListAsync(ct);

        return await productDtoFactory.CreateShortDtosAsync(products, ct);
    }
}

public record GlobalCategoryDto(Guid Id, string Name, string Slug, int SortOrder);
