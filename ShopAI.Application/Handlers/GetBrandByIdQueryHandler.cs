using AutoMapper;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetBrandByIdQuery(Guid Id) : IRequest<BrandDto>;
    public class GetBrandByIdHandler(
    IBrandRepository brandRepository,
    IMapper mapper) : IRequestHandler<GetBrandByIdQuery, BrandDto>
    {
        public async Task<BrandDto> Handle(GetBrandByIdQuery request, CancellationToken ct)
        {
            // Ищем бренд по ID
            var brand = await brandRepository.GetByIdAsync(request.Id);

            // Если не нашли — выбрасываем исключение, которое поймает контроллер и вернет 404
            if (brand == null)
            {
                throw new KeyNotFoundException("Бренд не найден.");
            }

            // Мапим сущность в DTO и возвращаем
            return mapper.Map<BrandDto>(brand);
        }
    }
}
