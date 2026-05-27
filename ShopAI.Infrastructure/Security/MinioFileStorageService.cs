using Minio;
using Minio.DataModel.Args;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Infrastructure.Security;

public class MinioFileStorageService(IMinioClient minioClient) : IFileStorageService
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
        return await minioClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithExpiry(expirySeconds));
    }

    public async Task DeleteAsync(string bucketName, string objectName, CancellationToken ct = default)
    {
        await minioClient.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucketName).WithObject(objectName), ct);
    }
}
