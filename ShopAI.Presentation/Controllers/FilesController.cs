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

    [HttpPost("products/{productId:guid}/image")]
    public async Task<ActionResult<object>> UploadProductImage(Guid productId, IFormFile file, CancellationToken ct)
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

    [HttpPost("shops/{shopId:guid}/logo")]
    public async Task<ActionResult<object>> UploadShopLogo(Guid shopId, IFormFile file, CancellationToken ct)
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

    [HttpGet("{id:guid}/url")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetPresignedUrl(Guid id)
    {
        var metadata = await fileMetadataRepository.GetByIdAsync(id);
        if (metadata == null) return NotFound();

        var url = await fileStorageService.GetPresignedUrlAsync(metadata.Bucket, metadata.ObjectName);
        return Ok(new { url });
    }

    [HttpDelete("{id:guid}")]
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
}
