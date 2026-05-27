using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Handlers;

public record GetShopByIdQuery(Guid Id) : IRequest<ShopDto>;

public class GetShopByIdHandler(
    IShopRepository shopRepository,
    IUserRepository userRepository,
    IFileStorageService fileStorageService,
    IConfiguration configuration,
    IMapper mapper)
    : IRequestHandler<GetShopByIdQuery, ShopDto>
{
    public async Task<ShopDto> Handle(GetShopByIdQuery request, CancellationToken ct)
    {
        var shop = await shopRepository.GetByIdAsync(request.Id);

        if (shop == null)
        {
            throw new KeyNotFoundException($"Магазин с ID {request.Id} не найден.");
        }

        _ = await userRepository.GetByIdAsync(shop.OwnerId);

        var dto = mapper.Map<ShopDto>(shop);
        return dto with { LogoPath = await ResolveUrlAsync(dto.LogoPath) };
    }

    private async Task<string> ResolveUrlAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;
        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        return await fileStorageService.GetPresignedUrlAsync(bucket, value);
    }
}
