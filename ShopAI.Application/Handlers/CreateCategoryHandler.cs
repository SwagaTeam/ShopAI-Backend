using Domain.Entities;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record CreateCategoryCommand(string Name, Guid ShopId, Guid? ParentId = null) : IRequest<Guid>;

public class CreateCategoryHandler(ICategoryRepository categoryRepository) 
    : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        var category = new Category(request.Name, request.ShopId, request.ParentId);
        
        await categoryRepository.AddAsync(category);
        await categoryRepository.SaveAsync(ct);
        
        return category.Id;
    }
}