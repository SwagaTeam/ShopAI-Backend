using System.ComponentModel;
using System.Text.Json;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;
using ShopAI.Infrastructure;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Requests;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")] 
public class ProductsController(
    IMediator mediator,
    IProductRepository productRepository,
    IFileMetadataRepository fileMetadataRepository,
    IFileStorageService fileStorageService,
    AppDbContext context,
    IConfiguration configuration) : ControllerBase
{
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    /// <summary>
    /// Получение данных для главной страницы.
    /// </summary>
    /// <remarks>
    /// Возвращает два списка: самые новые товары и самые популярные (на основе рейтинга/продаж).
    /// </remarks>
    /// <param name="count">Количество товаров в каждом списке (минимум 1, максимум 50). По умолчанию: 10.</param>
    /// <returns>Объект с двумя списками товаров.</returns>
    /// <response code="200">Данные успешно получены.</response>
    /// <response code="400">Некорректный параметр count.</response>
    [HttpGet("main-page")]
    [ProducesResponseType(typeof(MainPageProductsVm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MainPageProductsVm>> GetMainPage(
        [FromQuery, DefaultValue(10)] int count = 10)
    {
        if (count <= 0) return BadRequest("Count must be greater than 0");
        
        var query = new GetMainPageProductsQuery(count);
        var result = await mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Создание нового товара и привязка его к магазину.
    /// </summary>
    /// <param name="command">Данные товара (название, цена, категория, магазин и т.д.)</param>
    /// <returns>Идентификатор созданного товара.</returns>
    /// <response code="201">Товар успешно создан.</response>
    /// <response code="400">Ошибка валидации входных данных.</response>
    /// <response code="404">Указанный магазин или категория не найдены.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var productId = await mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = productId }, productId);
    }

    [HttpPost("with-image")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> CreateWithImage(
        [FromForm] CreateProductWithImageRequest request,
        CancellationToken ct)
    {
        Dictionary<string, string>? attributes;
        List<string>? tags;
        var files = GetImageFiles(request);

        try
        {
            attributes = ParseAttributes(request.AttributesJson);
            tags = ParseTags(request.Tags, request.TagsCsv);
            foreach (var file in files)
                ValidateImage(file);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var productId = await mediator.Send(new CreateProductCommand(
            request.ShopId,
            request.Name,
            request.Price,
            request.CategoryId,
            request.Description ?? string.Empty,
            string.Empty,
            request.StockQuantity,
            request.BrandId,
            tags,
            attributes), ct);

        if (files.Count > 0)
        {
            var product = await productRepository.GetByIdAsync(productId);
            if (product == null) return NotFound("Product not found after creation.");

            var uploaded = new List<FileMetadata>(files.Count);
            foreach (var file in files)
            {
                uploaded.Add(await UploadProductImageAsync(file, productId, ct));
            }

            product.ImageUrl = uploaded[0].ObjectName;
            productRepository.Update(product);
            await productRepository.SaveAsync(ct);
        }

        return CreatedAtAction(nameof(GetById), new { id = productId }, productId);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var product = await context.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        try
        {
            await ApplyProductUpdateAsync(product, request, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/with-images")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWithImages(
        Guid id,
        [FromForm] UpdateProductWithImagesRequest request,
        CancellationToken ct)
    {
        var product = await context.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        var files = GetImageFiles(request);

        try
        {
            foreach (var file in files)
                ValidateImage(file);

            await ApplyProductUpdateAsync(product, request, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (request.ReplaceImages || request.ClearImages)
        {
            await DeleteProductImagesAsync(product.Id, ct);
            product.ImageUrl = string.Empty;
        }

        if (files.Count > 0)
        {
            var uploaded = new List<FileMetadata>(files.Count);
            foreach (var file in files)
            {
                uploaded.Add(await UploadProductImageAsync(file, product.Id, ct));
            }

            product.ImageUrl = uploaded[0].ObjectName;
        }

        await context.SaveChangesAsync(ct);
        return NoContent();
    }


    [HttpPut("{id:guid}/tags")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetTags(Guid id, [FromBody] List<string> tags)
    {
        await mediator.Send(new SetProductTagsCommand(id, tags));
        return NoContent();
    }

    [HttpPost("{id:guid}/tags")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddTags(Guid id, [FromBody] List<string> tags)
    {
        await mediator.Send(new AddProductTagsCommand(id, tags));
        return NoContent();
    }

    [HttpDelete("{id:guid}/tags")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveTags(Guid id, [FromBody] List<string> tags)
    {
        await mediator.Send(new RemoveProductTagsCommand(id, tags));
        return NoContent();
    }

    [HttpPut("{id:guid}/attributes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetAttributes(Guid id, [FromBody] Dictionary<string, string> attributes)
    {
        await mediator.Send(new SetProductAttributesCommand(id, attributes));
        return NoContent();
    }

    [HttpDelete("{id:guid}/attributes/{key}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveAttribute(Guid id, string key)
    {
        await mediator.Send(new RemoveProductAttributeCommand(id, key));
        return NoContent();
    }

    /// <summary>
    /// Удалить товар по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор товара (GUID).</param>
    /// <response code="204">Товар успешно удален.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await mediator.Send(new DeleteProductCommand(id));
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Получение детальной информации о конкретном товаре по его ID.
    /// </summary>
    /// <param name="id">Идентификатор товара (GUID).</param>
    /// <response code="200">Данные товара успешно получены.</response>
    /// <response code="404">Товар с указанным ID не найден.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetById(Guid id)
    {
        try
        {
            var result = await mediator.Send(new GetProductByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    /// <summary>
    /// Получение детальной информации о конкретном товаре по его категории.
    /// </summary>
    /// <param name="categoryId">Идентификатор категории (GUID).</param>
    /// <response code="200">Данные товара успешно получены.</response>
    /// <response code="404">Товар с указанным ID не найден.</response>
    [HttpGet("get-by-category/{categoryId:guid}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ProductDetailsDto>>> GetByCategoryId(Guid categoryId)
    {
        try
        {
            var result = await mediator.Send(new GetProductByCategoryIdQuery(categoryId));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    /// <summary>
    /// Получение детальной информации о конкретном товаре по его магазину и категории.
    /// </summary>
    /// <param name="categoryId">Идентификатор категории (GUID).</param>
    /// <param name="shopId">Идентификатор категории (GUID).</param>
    /// <response code="200">Данные товара успешно получены.</response>
    /// <response code="404">Товар с указанным ID не найден.</response>
    [HttpGet("get-by-category-and-shop-id/{categoryId:guid}/{shopId:guid}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetByShopAndCategoryId(Guid categoryId, Guid shopId)
    {
        try
        {
            var result = await mediator.Send(new GetProductByShopAndCategoryIdsQuery(categoryId, shopId));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    // WebApi/Controllers/ProductsController.cs
    /// <summary>
    /// Получение списка товаров с фильтрацией, сортировкой и пагинацией.
    /// </summary>
    /// <param name="request">Параметры фильтрации</param>
    /// <response code="200">Список товаров успешно получен</response>
    [HttpGet("filter")]
    [ProducesResponseType(typeof(PagedResult<ProductDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDetailsDto>>> GetProductsByFilters(
        [FromQuery] GetProductsByFiltersRequest request)
    {
        var query = new GetProductsByFiltersQuery(request);
        var result = await mediator.Send(query);
        return Ok(result);
    }
    

    private async Task ApplyProductUpdateAsync(Product product, IUpdateProductRequest request, CancellationToken ct)
    {
        if (request.ShopId.HasValue && !await context.Shops.AnyAsync(s => s.Id == request.ShopId.Value, ct))
            throw new ArgumentException("Shop was not found.");

        if (request.CategoryId.HasValue && !await context.Categories.AnyAsync(c => c.Id == request.CategoryId.Value, ct))
            throw new ArgumentException("Category was not found.");

        if (request.BrandId.HasValue && !await context.Brands.AnyAsync(b => b.Id == request.BrandId.Value, ct))
            throw new ArgumentException("Brand was not found.");

        var nextShopId = request.ShopId ?? product.ShopId;
        var nextCategoryId = request.CategoryId ?? product.CategoryId;

        var categoryBelongsToShop = await context.Categories
            .AnyAsync(c => c.Id == nextCategoryId && c.ShopId == nextShopId, ct);
        if (!categoryBelongsToShop)
            throw new InvalidOperationException("Category does not belong to the selected shop.");

        if (request.Price.HasValue && request.Price.Value <= 0)
            throw new ArgumentException("Product price must be greater than zero.");

        if (request.StockQuantity.HasValue && request.StockQuantity.Value < 0)
            throw new ArgumentException("Stock quantity cannot be negative.");

        var nextName = request.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(nextName))
        {
            var duplicate = await context.Products.AnyAsync(p =>
                p.Id != product.Id &&
                p.ShopId == nextShopId &&
                p.Name.ToLower() == nextName.ToLower(), ct);
            if (duplicate)
                throw new InvalidOperationException("Product with this name already exists in the selected shop.");

            product.Name = nextName;
        }

        if (request.ShopId.HasValue) product.ShopId = request.ShopId.Value;
        if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;
        if (request.BrandIdSet) product.BrandId = request.BrandId;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.StockQuantity.HasValue) product.StockQuantity = request.StockQuantity.Value;
        if (request.Description != null) product.Description = request.Description.Trim();
        if (request.ImageUrl != null) product.ImageUrl = request.ImageUrl.Trim();

        if (request.Tags != null || request.TagsCsv != null)
        {
            product.Tags = string.Join(",", ParseTags(request.Tags, request.TagsCsv) ?? []);
        }

        if (request.Attributes != null)
        {
            product.AttributesJson = JsonSerializer.Serialize(request.Attributes);
        }
        else if (request.AttributesJson != null)
        {
            product.AttributesJson = JsonSerializer.Serialize(ParseAttributes(request.AttributesJson) ?? new Dictionary<string, string>());
        }
    }

    private async Task<FileMetadata> UploadProductImageAsync(IFormFile file, Guid productId, CancellationToken ct)
    {
        ValidateImage(file);

        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        await fileStorageService.EnsureBucketExistsAsync(bucket, ct);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var objectName = $"products/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();
        await fileStorageService.UploadAsync(bucket, objectName, stream, file.Length, file.ContentType, ct);

        var metadata = new FileMetadata
        {
            Bucket = bucket,
            ObjectName = objectName,
            ContentType = file.ContentType,
            Size = file.Length,
            OriginalFileName = Path.GetFileName(file.FileName),
            CreatedAt = DateTime.UtcNow,
            ProductId = productId
        };

        await fileMetadataRepository.AddAsync(metadata);
        await fileMetadataRepository.SaveAsync(ct);
        return metadata;
    }

    private async Task DeleteProductImagesAsync(Guid productId, CancellationToken ct)
    {
        var images = await context.FileMetadatas
            .Where(f => f.ProductId == productId)
            .ToListAsync(ct);

        foreach (var image in images)
        {
            await fileStorageService.DeleteAsync(image.Bucket, image.ObjectName, ct);
        }

        context.FileMetadatas.RemoveRange(images);
    }

    private static void ValidateImage(IFormFile file)
    {
        if (file.Length == 0) throw new ArgumentException("File is empty.");
        if (!AllowedImageContentTypes.Contains(file.ContentType)) throw new ArgumentException("Invalid image content type.");
        if (file.Length > 5 * 1024 * 1024) throw new ArgumentException("File too large. Max: 5MB.");
    }

    private static List<IFormFile> GetImageFiles(CreateProductWithImageRequest request)
    {
        var files = new List<IFormFile>();
        if (request.Image != null) files.Add(request.Image);
        if (request.File != null) files.Add(request.File);
        if (request.Images != null) files.AddRange(request.Images.Where(f => f != null));
        if (request.Files != null) files.AddRange(request.Files.Where(f => f != null));
        return files.Where(f => f.Length > 0).ToList();
    }

    private static List<IFormFile> GetImageFiles(UpdateProductWithImagesRequest request)
    {
        var files = new List<IFormFile>();
        if (request.Image != null) files.Add(request.Image);
        if (request.File != null) files.Add(request.File);
        if (request.Images != null) files.AddRange(request.Images.Where(f => f != null));
        if (request.Files != null) files.AddRange(request.Files.Where(f => f != null));
        return files.Where(f => f.Length > 0).ToList();
    }

    private static Dictionary<string, string>? ParseAttributes(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson)) return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("attributesJson must be a JSON object with string values.", ex);
        }
    }

    private static List<string>? ParseTags(List<string>? tags, string? tagsCsv)
    {
        var values = new List<string>();
        if (tags != null) values.AddRange(tags);
        if (!string.IsNullOrWhiteSpace(tagsCsv))
            values.AddRange(tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var normalized = values
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        return normalized.Count == 0 ? null : normalized;
    }
}

public class CreateProductWithImageRequest
{
    public Guid ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public string? Description { get; set; }
    public int StockQuantity { get; set; }
    public Guid? BrandId { get; set; }
    public List<string>? Tags { get; set; }
    public string? TagsCsv { get; set; }
    public string? AttributesJson { get; set; }
    public IFormFile? Image { get; set; }
    public IFormFile? File { get; set; }
    public List<IFormFile>? Images { get; set; }
    public List<IFormFile>? Files { get; set; }
}

public interface IUpdateProductRequest
{
    Guid? ShopId { get; }
    string? Name { get; }
    decimal? Price { get; }
    Guid? CategoryId { get; }
    string? Description { get; }
    string? ImageUrl { get; }
    int? StockQuantity { get; }
    Guid? BrandId { get; }
    bool BrandIdSet { get; }
    List<string>? Tags { get; }
    string? TagsCsv { get; }
    string? AttributesJson { get; }
    Dictionary<string, string>? Attributes { get; }
}

public class UpdateProductRequest : IUpdateProductRequest
{
    public Guid? ShopId { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; }
    public Guid? BrandId { get; set; }
    public bool ClearBrand { get; set; }
    public bool BrandIdSet => BrandId.HasValue || ClearBrand;
    public List<string>? Tags { get; set; }
    public string? TagsCsv { get; set; }
    public string? AttributesJson { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
}

public class UpdateProductWithImagesRequest : IUpdateProductRequest
{
    public Guid? ShopId { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; }
    public Guid? BrandId { get; set; }
    public bool ClearBrand { get; set; }
    public bool BrandIdSet => BrandId.HasValue || ClearBrand;
    public List<string>? Tags { get; set; }
    public string? TagsCsv { get; set; }
    public string? AttributesJson { get; set; }
    public Dictionary<string, string>? Attributes => null;
    public bool ReplaceImages { get; set; }
    public bool ClearImages { get; set; }
    public IFormFile? Image { get; set; }
    public IFormFile? File { get; set; }
    public List<IFormFile>? Images { get; set; }
    public List<IFormFile>? Files { get; set; }
}
