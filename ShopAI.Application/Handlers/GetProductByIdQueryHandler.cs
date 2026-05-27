using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Handlers
{
    public record GetProductByIdQuery(Guid Id) : IRequest<ProductDetailsDto>;

    public class GetProductByIdHandler(
        IProductRepository productRepository,
        IFileStorageService fileStorageService,
        IConfiguration configuration,
        IMapper mapper) : IRequestHandler<GetProductByIdQuery, ProductDetailsDto>
    {
        public async Task<ProductDetailsDto> Handle(GetProductByIdQuery request, CancellationToken ct)
        {
            var product = await productRepository.GetByIdWithDetailsAsync(request.Id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Товар с ID {request.Id} не найден.");
            }

            var dto = mapper.Map<ProductDetailsDto>(product);
            dto = dto with { ImageUrl = await ResolveUrlAsync(dto.ImageUrl) };
            return dto;
        }

        private async Task<string> ResolveUrlAsync(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;
            var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
            return await fileStorageService.GetPresignedUrlAsync(bucket, value);
        }
    }
}
