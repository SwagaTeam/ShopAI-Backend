using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record GetProductByShopAndCategoryIdsQuery(Guid ShopId, Guid CategoryId) : IRequest<List<ProductDetailsDto>>;

public class GetProductByShopAndCategoryIdsQueryHandler(
    IProductRepository productRepository,
    IProductDtoFactory productDtoFactory) : IRequestHandler<GetProductByShopAndCategoryIdsQuery, List<ProductDetailsDto>>
{
    public async Task<List<ProductDetailsDto>> Handle(GetProductByShopAndCategoryIdsQuery request, CancellationToken ct)
    {
        var products = await productRepository.GetProductsByShopAndCategoryAsync(request.ShopId, request.CategoryId);

        if (products == null)
        {
            throw new KeyNotFoundException($"Product with Shop Id and Category ID {request.ShopId} {request.CategoryId} was not found.");
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
