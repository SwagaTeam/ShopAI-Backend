using AutoMapper;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetAllBrandsQuery() : IRequest<List<BrandDto>>;
    public class GetAllBrandsQueryHandler(
    IBrandRepository brandRepository,
    IMapper mapper) : IRequestHandler<GetAllBrandsQuery, List<BrandDto>>
    {
        public async Task<List<BrandDto>> Handle(GetAllBrandsQuery request, CancellationToken ct)
        {
            // Получаем все бренды из базы
            var brands = await brandRepository.GetAllAsync();

            // Мапим коллекцию сущностей в коллекцию DTO и возвращаем
            return mapper.Map<List<BrandDto>>(brands);
        }
    }
}
