using Domain.Entities;
using ShopAI.Application.Models;

namespace ShopAI.Application.Helpers.Abstractions;

public interface IProductDtoFactory
{
    Task<List<ProductShortDto>> CreateShortDtosAsync(IReadOnlyCollection<Product> products, CancellationToken ct = default);
    Task<ProductDetailsDto> CreateDetailsDtoAsync(Product product, CancellationToken ct = default);
}
