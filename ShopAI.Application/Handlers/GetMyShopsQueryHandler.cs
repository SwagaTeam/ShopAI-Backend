using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Application.Handlers
{
    public record GetMyShopsQuery() : IRequest<List<ShopDto>>;

    public class GetMyShopsHandler(
        IShopRepository shopRepository,
        IUserContext userContext,
        IFileStorageService fileStorageService,
        IConfiguration configuration,
        IMapper mapper) : IRequestHandler<GetMyShopsQuery, List<ShopDto>>
    {
        public async Task<List<ShopDto>> Handle(GetMyShopsQuery request, CancellationToken ct)
        {
            var userId = userContext.UserId;
            var shops = await shopRepository.GetByOwnerIdAsync(userId, ct);
            var dtos = mapper.Map<List<ShopDto>>(shops);

            var result = new List<ShopDto>(dtos.Count);
            foreach (var dto in dtos)
            {
                result.Add(dto with { LogoPath = await ResolveUrlAsync(dto.LogoPath) });
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
}
