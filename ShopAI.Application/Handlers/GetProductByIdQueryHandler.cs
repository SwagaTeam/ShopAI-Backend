using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record GetProductByIdQuery(Guid Id) : IRequest<ProductDetailsDto>;

public class GetProductByIdHandler(
    IProductRepository productRepository,
    IProductDtoFactory productDtoFactory) : IRequestHandler<GetProductByIdQuery, ProductDetailsDto>
{
    public async Task<ProductDetailsDto> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdWithDetailsAsync(request.Id);

        if (product == null)
        {
            throw new KeyNotFoundException($"Product with ID {request.Id} was not found.");
        }

        return await productDtoFactory.CreateDetailsDtoAsync(product, ct);
    }
}
