using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Handlers;

public record MainPageProductsVm(List<ProductShortDto> Latest, List<ProductShortDto> Popular);
public record GetMainPageProductsQuery(int Count) : IRequest<MainPageProductsVm>;

public class GetMainPageProductsQueryHandler(
    IProductRepository productRepository,
    IFileStorageService fileStorageService,
    IConfiguration configuration,
    IMapper mapper)
    : IRequestHandler<GetMainPageProductsQuery, MainPageProductsVm>
{
    public async Task<MainPageProductsVm> Handle(GetMainPageProductsQuery request, CancellationToken ct)
    {
        var count = request.Count switch
        {
            <= 0 => 10,
            > 100 => 100,
            _ => request.Count
        };

        var latestEntities = await productRepository.GetLatestAsync(count);
        var popularEntities = await productRepository.GetPopularAsync(count);

        var latest = mapper.Map<List<ProductShortDto>>(latestEntities);
        var popular = mapper.Map<List<ProductShortDto>>(popularEntities);

        latest = await ResolveUrlsAsync(latest);
        popular = await ResolveUrlsAsync(popular);

        return new MainPageProductsVm(latest, popular);
    }

    private async Task<List<ProductShortDto>> ResolveUrlsAsync(List<ProductShortDto> items)
    {
        var result = new List<ProductShortDto>(items.Count);
        foreach (var item in items)
        {
            var url = await ResolveUrlAsync(item.ImageUrl);
            result.Add(item with { ImageUrl = url });
        }

        return result;
    }

    private async Task<string> ResolveUrlAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return value;
        var bucket = configuration["Minio:Bucket"] ?? "shopai-images";
        return await fileStorageService.GetPresignedUrlAsync(bucket, value);
    }
}
