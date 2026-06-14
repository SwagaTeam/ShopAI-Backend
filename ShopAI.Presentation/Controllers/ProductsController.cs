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

    /// <summary>
    /// Создать товар вместе с одной или несколькими фотографиями.
    /// </summary>
    /// <remarks>
    /// Принимает multipart/form-data. Данные товара передаются обычными form-полями, изображения - в полях image/file или images/files.
    /// Первое успешно загруженное изображение становится основной картинкой товара.
    /// </remarks>
    /// <param name="request">Данные создаваемого товара, теги, характеристики и загружаемые изображения.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Идентификатор созданного товара.</returns>
    /// <response code="201">Товар создан, изображения при наличии загружены.</response>
    /// <response code="400">Некорректные поля товара, JSON характеристик или файл изображения.</response>
    /// <response code="404">Указанный магазин, категория или бренд не найдены.</response>
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

    /// <summary>
    /// Обновить данные товара без загрузки файлов.
    /// </summary>
    /// <remarks>
    /// Все поля в теле запроса опциональны: переданные значения заменяют текущие данные товара, отсутствующие поля остаются без изменений.
    /// </remarks>
    /// <param name="id">Идентификатор товара, который нужно обновить.</param>
    /// <param name="request">Новые значения полей товара: магазин, категория, бренд, цена, остаток, описание, теги и характеристики.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="204">Товар успешно обновлен.</response>
    /// <response code="400">Некорректные данные, нарушена связка магазина и категории или найден дубль названия.</response>
    /// <response code="404">Товар не найден.</response>
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

    /// <summary>
    /// Обновить товар и управлять его изображениями.
    /// </summary>
    /// <remarks>
    /// Маршрут: PUT /api/Products/{id}/with-images.
    ///
    /// Используется для редактирования карточки товара через multipart/form-data, когда вместе с текстовыми полями нужно загрузить новые изображения,
    /// заменить текущие изображения или полностью очистить галерею.
    ///
    /// Параметр пути id - GUID существующего товара. В form-data можно передать любые поля товара: shopId, name, price, categoryId, description,
    /// imageUrl, stockQuantity, brandId, clearBrand, tags/tagsCsv и attributesJson. Неуказанные поля остаются без изменений.
    ///
    /// Изображения можно отправлять в одно из совместимых полей: image или file для одного файла, images или files для списка файлов.
    /// Разрешены только image/jpeg, image/png и image/webp, размер каждого файла - до 5 МБ.
    ///
    /// replaceImages=true сначала удаляет все старые изображения товара, затем сохраняет новые файлы.
    /// clearImages=true удаляет все старые изображения и очищает основную картинку; если одновременно переданы новые файлы, они будут загружены после очистки.
    /// Если replaceImages и clearImages не переданы, новые изображения добавляются к существующим, а первая новая картинка становится основной.
    /// </remarks>
    /// <param name="id">Идентификатор обновляемого товара.</param>
    /// <param name="request">Multipart/form-data с новыми полями товара, флагами управления изображениями и файлами.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="204">Товар и изображения успешно обновлены.</response>
    /// <response code="400">Некорректные поля товара, JSON характеристик, связь магазина и категории или файл изображения.</response>
    /// <response code="404">Товар не найден.</response>
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


    /// <summary>
    /// Полностью заменить набор тегов товара.
    /// </summary>
    /// <param name="id">Идентификатор товара, для которого заменяются теги.</param>
    /// <param name="tags">Новый список тегов. Пустой список очищает теги товара.</param>
    /// <response code="204">Теги товара успешно заменены.</response>
    [HttpPut("{id:guid}/tags")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetTags(Guid id, [FromBody] List<string> tags)
    {
        await mediator.Send(new SetProductTagsCommand(id, tags));
        return NoContent();
    }

    /// <summary>
    /// Добавить теги к товару без удаления существующих.
    /// </summary>
    /// <param name="id">Идентификатор товара, к которому добавляются теги.</param>
    /// <param name="tags">Список новых тегов. Дубликаты будут нормализованы и отброшены.</param>
    /// <response code="204">Теги успешно добавлены.</response>
    [HttpPost("{id:guid}/tags")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddTags(Guid id, [FromBody] List<string> tags)
    {
        await mediator.Send(new AddProductTagsCommand(id, tags));
        return NoContent();
    }

    /// <summary>
    /// Удалить указанные теги из товара.
    /// </summary>
    /// <param name="id">Идентификатор товара, у которого удаляются теги.</param>
    /// <param name="tags">Список тегов, которые нужно убрать из товара.</param>
    /// <response code="204">Указанные теги удалены или уже отсутствовали.</response>
    [HttpDelete("{id:guid}/tags")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTags(Guid id, [FromBody] List<string> tags)
    {
        await mediator.Send(new RemoveProductTagsCommand(id, tags));
        return NoContent();
    }

    /// <summary>
    /// Полностью заменить характеристики товара.
    /// </summary>
    /// <param name="id">Идентификатор товара, для которого задаются характеристики.</param>
    /// <param name="attributes">Словарь характеристик, где ключ - название характеристики, значение - ее значение.</param>
    /// <response code="204">Характеристики товара успешно заменены.</response>
    [HttpPut("{id:guid}/attributes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetAttributes(Guid id, [FromBody] Dictionary<string, string> attributes)
    {
        await mediator.Send(new SetProductAttributesCommand(id, attributes));
        return NoContent();
    }

    /// <summary>
    /// Удалить одну характеристику товара по ключу.
    /// </summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="key">Ключ характеристики, которую нужно удалить.</param>
    /// <response code="204">Характеристика удалена или уже отсутствовала.</response>
    [HttpDelete("{id:guid}/attributes/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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
    /// <param name="shopId">Идентификатор магазина (GUID).</param>
    /// <response code="200">Данные товара успешно получены.</response>
    /// <response code="404">Товар с указанным ID не найден.</response>
    [HttpGet("get-by-category-and-shop-id/{categoryId:guid}/{shopId:guid}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetByShopAndCategoryId(Guid categoryId, Guid shopId)
    {
        try
        {
            var result = await mediator.Send(new GetProductByShopAndCategoryIdsQuery(shopId, categoryId));
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

/// <summary>
/// Multipart-запрос на создание товара с изображениями.
/// </summary>
public class CreateProductWithImageRequest
{
    /// <summary>Идентификатор магазина, в котором создается товар.</summary>
    public Guid ShopId { get; set; }

    /// <summary>Название товара. Должно быть уникальным внутри выбранного магазина.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Цена товара. Должна быть больше нуля.</summary>
    public decimal Price { get; set; }

    /// <summary>Идентификатор категории товара. Категория должна принадлежать указанному магазину.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Описание товара, отображаемое в карточке.</summary>
    public string? Description { get; set; }

    /// <summary>Количество товара на складе. Не может быть отрицательным.</summary>
    public int StockQuantity { get; set; }

    /// <summary>Необязательный идентификатор бренда товара.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Список тегов товара. Значения нормализуются и сохраняются без дублей.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Теги одной строкой через запятую. Можно использовать вместе с Tags.</summary>
    public string? TagsCsv { get; set; }

    /// <summary>Характеристики товара в формате JSON-объекта, например {"color":"black","memory":"128GB"}.</summary>
    public string? AttributesJson { get; set; }

    /// <summary>Один файл изображения. Разрешены JPEG, PNG и WebP до 5 МБ.</summary>
    public IFormFile? Image { get; set; }

    /// <summary>Альтернативное поле для одного файла изображения. Используется для совместимости с клиентами.</summary>
    public IFormFile? File { get; set; }

    /// <summary>Список изображений товара. Первое загруженное изображение станет основным.</summary>
    public List<IFormFile>? Images { get; set; }

    /// <summary>Альтернативное поле для списка изображений. Используется для совместимости с клиентами.</summary>
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

/// <summary>
/// Запрос на частичное обновление товара без загрузки файлов.
/// </summary>
public class UpdateProductRequest : IUpdateProductRequest
{
    /// <summary>Новый магазин товара. Если не передан, магазин не меняется.</summary>
    public Guid? ShopId { get; set; }

    /// <summary>Новое название товара. Проверяется на уникальность внутри выбранного магазина.</summary>
    public string? Name { get; set; }

    /// <summary>Новая цена товара. Должна быть больше нуля.</summary>
    public decimal? Price { get; set; }

    /// <summary>Новая категория товара. Категория должна принадлежать итоговому магазину товара.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Новое описание товара.</summary>
    public string? Description { get; set; }

    /// <summary>Новый путь или URL основного изображения без загрузки файла.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Новое количество товара на складе. Не может быть отрицательным.</summary>
    public int? StockQuantity { get; set; }

    /// <summary>Новый бренд товара. Если нужно очистить бренд, используйте ClearBrand.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Если true, удаляет привязку товара к бренду.</summary>
    public bool ClearBrand { get; set; }

    public bool BrandIdSet => BrandId.HasValue || ClearBrand;

    /// <summary>Новый полный список тегов товара.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Новый полный список тегов одной строкой через запятую.</summary>
    public string? TagsCsv { get; set; }

    /// <summary>Новые характеристики товара в формате JSON-объекта со строковыми значениями.</summary>
    public string? AttributesJson { get; set; }

    /// <summary>Новые характеристики товара как JSON-объект в теле запроса.</summary>
    public Dictionary<string, string>? Attributes { get; set; }
}

/// <summary>
/// Multipart-запрос на частичное обновление товара и управление его изображениями.
/// </summary>
public class UpdateProductWithImagesRequest : IUpdateProductRequest
{
    /// <summary>Новый магазин товара. Если не передан, магазин не меняется.</summary>
    public Guid? ShopId { get; set; }

    /// <summary>Новое название товара. Проверяется на уникальность внутри итогового магазина.</summary>
    public string? Name { get; set; }

    /// <summary>Новая цена товара. Должна быть больше нуля.</summary>
    public decimal? Price { get; set; }

    /// <summary>Новая категория товара. Категория должна принадлежать итоговому магазину товара.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Новое описание товара.</summary>
    public string? Description { get; set; }

    /// <summary>Новый путь или URL основного изображения без загрузки файла.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Новое количество товара на складе. Не может быть отрицательным.</summary>
    public int? StockQuantity { get; set; }

    /// <summary>Новый бренд товара. Если нужно очистить бренд, используйте ClearBrand.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Если true, удаляет привязку товара к бренду.</summary>
    public bool ClearBrand { get; set; }

    public bool BrandIdSet => BrandId.HasValue || ClearBrand;

    /// <summary>Новый полный список тегов товара.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Новый полный список тегов одной строкой через запятую.</summary>
    public string? TagsCsv { get; set; }

    /// <summary>Новые характеристики товара в формате JSON-объекта со строковыми значениями.</summary>
    public string? AttributesJson { get; set; }

    public Dictionary<string, string>? Attributes => null;

    /// <summary>Если true, перед загрузкой новых файлов удаляет все старые изображения товара.</summary>
    public bool ReplaceImages { get; set; }

    /// <summary>Если true, удаляет все старые изображения и очищает основную картинку товара.</summary>
    public bool ClearImages { get; set; }

    /// <summary>Один новый файл изображения. Разрешены JPEG, PNG и WebP до 5 МБ.</summary>
    public IFormFile? Image { get; set; }

    /// <summary>Альтернативное поле для одного нового файла изображения. Используется для совместимости с клиентами.</summary>
    public IFormFile? File { get; set; }

    /// <summary>Список новых изображений товара.</summary>
    public List<IFormFile>? Images { get; set; }

    /// <summary>Альтернативное поле для списка новых изображений. Используется для совместимости с клиентами.</summary>
    public List<IFormFile>? Files { get; set; }
}
