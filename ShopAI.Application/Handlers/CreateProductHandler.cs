using Domain.Entities;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record CreateProductCommand(
    Guid ShopId,
    string Name,
    decimal Price,
    Guid CategoryId,
    string Description,
    string ImageUrl,
    int StockQuantity,
    Guid? BrandId) : IRequest<Guid>;
    
public class CreateProductHandler(IProductRepository productRepository) 
    : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(
            request.ShopId,
            request.Name,
            request.Price,
            request.CategoryId,
            request.Description,
            request.ImageUrl,
            request.StockQuantity,
            request.BrandId
        );

        await productRepository.AddAsync(product);
        await productRepository.SaveAsync(cancellationToken);

        return product.Id;
    }
}