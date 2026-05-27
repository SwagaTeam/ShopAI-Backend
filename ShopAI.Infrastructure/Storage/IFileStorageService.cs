namespace ShopAI.Infrastructure.Storage;

public interface IFileStorageService
{
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default);
    Task<string> UploadAsync(string bucketName, string objectName, Stream stream, long size, string contentType, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expirySeconds = 3600);
    Task DeleteAsync(string bucketName, string objectName, CancellationToken ct = default);
}
