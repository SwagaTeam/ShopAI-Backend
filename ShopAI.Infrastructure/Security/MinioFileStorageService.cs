using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Infrastructure.Security;

public class MinioFileStorageService(IMinioClient minioClient, IConfiguration configuration) : IFileStorageService
{
    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default)
    {
        var exists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct);
        if (!exists)
        {
            await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);
        }
    }

    public async Task<string> UploadAsync(string bucketName, string objectName, Stream stream, long size, string contentType, CancellationToken ct = default)
    {
        await minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(contentType), ct);

        return objectName;
    }

    public async Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expirySeconds = 3600)
    {
        var url = await minioClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithExpiry(expirySeconds));

        return RewriteToPublicUrl(url);
    }

    public async Task DeleteAsync(string bucketName, string objectName, CancellationToken ct = default)
    {
        await minioClient.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucketName).WithObject(objectName), ct);
    }

    private string RewriteToPublicUrl(string presignedUrl)
    {
        var publicBaseUrl = configuration["Minio:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBaseUrl)
            || !Uri.TryCreate(presignedUrl, UriKind.Absolute, out var sourceUri)
            || !Uri.TryCreate(EnsureTrailingSlash(publicBaseUrl), UriKind.Absolute, out var publicUri))
        {
            return presignedUrl;
        }

        var basePath = publicUri.AbsolutePath.TrimEnd('/');
        var sourcePath = sourceUri.AbsolutePath.TrimStart('/');
        var builder = new UriBuilder(publicUri)
        {
            Path = string.IsNullOrEmpty(basePath) ? sourcePath : $"{basePath}/{sourcePath}",
            Query = sourceUri.Query.TrimStart('?')
        };

        return builder.Uri.ToString();
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }
}
