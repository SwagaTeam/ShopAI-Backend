using System.ComponentModel;
using System.Text.Json;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Application.Handlers;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
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
