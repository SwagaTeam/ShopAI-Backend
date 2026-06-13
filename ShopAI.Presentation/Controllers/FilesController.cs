using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class FilesController(
    IFileStorageService fileStorageService,
    IFileMetadataRepository fileMetadataRepository,
    IProductRepository productRepository,
    IShopRepository shopRepository,
    IConfiguration configuration) : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    /// <summary>
    /// Загрузить основное изображение товара.
    /// </summary>
    /// <param name="productId">Идентификатор товара, к которому привязывается изображение.</param>
    /// <param name="file">Файл изображения JPEG, PNG или WebP размером до 5 МБ.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Идентификатор файла и временная ссылка на просмотр.</returns>
    /// <response code="200">Изображение загружено и назначено основным для товара.</response>
    /// <response code="400">Файл пустой, слишком большой или имеет неподдерживаемый тип.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpPost("products/{productId:guid}/image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> UploadProductImage(
        Guid productId,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(productId);
        if (product == null) return NotFound("Product not found");

        var metadata = await UploadInternalAsync(file, "products", productId, null, ct);
        product.ImageUrl = metadata.ObjectName;
        productRepository.Update(product);
        await productRepository.SaveAsync(ct);

        var url = await fileStorageService.GetPresignedUrlAsync(metadata.Bucket, metadata.ObjectName);
        return Ok(new { metadata.Id, Url = url });
    }

    /// <summary>
    /// Загрузить несколько изображений товара.
    /// </summary>
    /// <remarks>
    /// Файлы можно передать в полях image/file для одного изображения или images/files для списка.
    /// Если у товара еще нет основного изображения, первая загруженная картинка станет основной.
    /// </remarks>
    /// <param name="productId">Идентификатор товара, к которому привязываются изображения.</param>
    /// <param name="request">Multipart-запрос с одним или несколькими файлами изображений.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Список идентификаторов загруженных файлов и временных ссылок.</returns>
    /// <response code="200">Изображения успешно загружены.</response>
    /// <response code="400">Файлы не переданы или один из файлов некорректен.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpPost("products/{productId:guid}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<object>>> UploadProductImages(
        Guid productId,
        [FromForm] ProductImagesUploadRequest request,
        CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(productId);
        if (product == null) return NotFound("Product not found");

        var files = GetImageFiles(request);
        if (files.Count == 0) return BadRequest("At least one image is required.");

        try
        {
            foreach (var file in files)
                ValidateFile(file);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var result = new List<object>(files.Count);
        FileMetadata? firstMetadata = null;
        foreach (var file in files)
        {
            var metadata = await UploadInternalAsync(file, "products", productId, null, ct);
            firstMetadata ??= metadata;
            var url = await fileStorageService.GetPresignedUrlAsync(metadata.Bucket, metadata.ObjectName);
            result.Add(new { metadata.Id, Url = url });
        }

        if (string.IsNullOrWhiteSpace(product.ImageUrl) && firstMetadata != null)
        {
            product.ImageUrl = firstMetadata.ObjectName;
            productRepository.Update(product);
            await productRepository.SaveAsync(ct);
        }

        return Ok(result);
    }

    /// <summary>
    /// Загрузить логотип магазина.
    /// </summary>
    /// <param name="shopId">Идентификатор магазина, для которого загружается логотип.</param>
    /// <param name="file">Файл логотипа JPEG, PNG или WebP размером до 5 МБ.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Идентификатор файла и временная ссылка на просмотр.</returns>
    /// <response code="200">Логотип загружен и привязан к магазину.</response>
    /// <response code="400">Файл пустой, слишком большой или имеет неподдерживаемый тип.</response>
    /// <response code="404">Магазин не найден.</response>
    [HttpPost("shops/{shopId:guid}/logo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> UploadShopLogo(
        Guid shopId,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        var shop = await shopRepository.GetByIdAsync(shopId);
        if (shop == null) return NotFound("Shop not found");

        var metadata = await UploadInternalAsync(file, "shops", null, shopId, ct);
        shop.LogoPath = metadata.ObjectName;
        shopRepository.Update(shop);
        await shopRepository.SaveAsync(ct);

        var url = await fileStorageService.GetPresignedUrlAsync(metadata.Bucket, metadata.ObjectName);
        return Ok(new { metadata.Id, Url = url });
    }

    /// <summary>
    /// Получить временную ссылку на файл.
    /// </summary>
    /// <param name="id">Идентификатор метаданных файла.</param>
    /// <returns>Временная presigned-ссылка для скачивания или просмотра файла.</returns>
    /// <response code="200">Ссылка успешно сформирована.</response>
    /// <response code="404">Файл не найден.</response>
    [HttpGet("{id:guid}/url")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetPresignedUrl(Guid id)
    {
        var metadata = await fileMetadataRepository.GetByIdAsync(id);
        if (metadata == null) return NotFound();

        var url = await fileStorageService.GetPresignedUrlAsync(metadata.Bucket, metadata.ObjectName);
        return Ok(new { url });
    }

    /// <summary>
    /// Удалить файл из хранилища и базы метаданных.
    /// </summary>
    /// <param name="id">Идентификатор метаданных файла.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="204">Файл успешно удален.</response>
    /// <response code="404">Файл не найден.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var metadata = await fileMetadataRepository.GetByIdAsync(id);
        if (metadata == null) return NotFound();

        await fileStorageService.DeleteAsync(metadata.Bucket, metadata.ObjectName, ct);
        fileMetadataRepository.Delete(metadata);
        await fileMetadataRepository.SaveAsync(ct);
        return NoContent();
    }

    private async Task<FileMetadata> UploadInternalAsync(
        IFormFile file,
        string folder,
        Guid? productId,
        Guid? shopId,
        CancellationToken ct)
    {
        ValidateFile(file);

        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        await fileStorageService.EnsureBucketExistsAsync(bucket, ct);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var objectName = $"{folder}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";

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
            ProductId = productId,
            ShopId = shopId
        };

        await fileMetadataRepository.AddAsync(metadata);
        await fileMetadataRepository.SaveAsync(ct);
        return metadata;
    }

    private static void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0) throw new ArgumentException("File is empty");
        if (!AllowedContentTypes.Contains(file.ContentType)) throw new ArgumentException("Invalid content type");
        if (file.Length > 5 * 1024 * 1024) throw new ArgumentException("File too large. Max: 5MB");
    }

    private static List<IFormFile> GetImageFiles(ProductImagesUploadRequest request)
    {
        var files = new List<IFormFile>();
        if (request.Image != null) files.Add(request.Image);
        if (request.File != null) files.Add(request.File);
        if (request.Images != null) files.AddRange(request.Images.Where(f => f != null));
        if (request.Files != null) files.AddRange(request.Files.Where(f => f != null));
        return files.Where(f => f.Length > 0).ToList();
    }
}

/// <summary>
/// Multipart-запрос на загрузку изображений товара.
/// </summary>
public class ProductImagesUploadRequest
{
    /// <summary>Один файл изображения JPEG, PNG или WebP размером до 5 МБ.</summary>
    public IFormFile? Image { get; set; }

    /// <summary>Альтернативное поле для одного файла изображения.</summary>
    public IFormFile? File { get; set; }

    /// <summary>Список файлов изображений JPEG, PNG или WebP размером до 5 МБ каждый.</summary>
    public List<IFormFile>? Images { get; set; }

    /// <summary>Альтернативное поле для списка файлов изображений.</summary>
    public List<IFormFile>? Files { get; set; }
}
