using AutoMapper;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    /// <summary>
    /// Запрос на получение детальной информации о товаре по его ID.
    /// </summary>
    public record GetProductByIdQuery(Guid Id) : IRequest<ProductDetailsDto>;

    public class GetProductByIdHandler(
    IProductRepository productRepository,
    IMapper mapper) : IRequestHandler<GetProductByIdQuery, ProductDetailsDto>
    {
        public async Task<ProductDetailsDto> Handle(GetProductByIdQuery request, CancellationToken ct)
        {
            // Получаем продукт из репозитория. 
            var product = await productRepository.GetByIdWithDetailsAsync(request.Id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Товар с ID {request.Id} не найден.");
            }

            return mapper.Map<ProductDetailsDto>(product);
        }
    }
}
