using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record GetProductByCategoryIdQuery(Guid CategoryId) : IRequest<List<ProductDetailsDto>>;

public class GetProductByCategoryHandler(
    IProductRepository productRepository,
    IProductDtoFactory productDtoFactory) : IRequestHandler<GetProductByCategoryIdQuery, List<ProductDetailsDto>>
{
    public async Task<List<ProductDetailsDto>> Handle(GetProductByCategoryIdQuery request, CancellationToken ct)
    {
        var products = await productRepository.GetProductsByCategoryAsync(request.CategoryId);

        if (products == null)
        {
            throw new KeyNotFoundException($"Product with Category ID {request.CategoryId} was not found.");
        }

        var result = new List<ProductDetailsDto>();
        foreach (var product in products)
        {
            var productDto = await productDtoFactory.CreateDetailsDtoAsync(product, ct);
            result.Add(productDto);
        }

        return result;
    }
}
