using Domain.Entities;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations;

public class FileMetadataRepository(AppDbContext context)
    : Repository<FileMetadata>(context), IFileMetadataRepository
{
}
