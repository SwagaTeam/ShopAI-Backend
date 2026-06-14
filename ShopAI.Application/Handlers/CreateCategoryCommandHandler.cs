using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на создание категории магазина.
/// </summary>
/// <param name="Name">Название категории.</param>
/// <param name="ShopId">Идентификатор магазина, к которому относится категория.</param>
/// <param name="ParentId">Необязательный идентификатор родительской категории для создания подкатегории.</param>
/// <param name="GlobalCategoryId">Optional site-wide category used for catalog navigation.</param>
public record CreateCategoryCommand(string Name, Guid ShopId, Guid? ParentId = null, Guid? GlobalCategoryId = null) : IRequest<Guid>;

public class CreateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    IShopRepository shopRepository,
    AppDbContext context)
    : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        if (!await shopRepository.ExistsAsync(request.ShopId, ct))
        {
            throw new ArgumentException($"Магазин с ID {request.ShopId} не найден.");
        }

        if (request.ParentId.HasValue)
        {
            var parentExists = await categoryRepository.ExistsAsync(request.ParentId.Value, ct);
            if (!parentExists)
            {
                throw new ArgumentException("Указанная родительская категория не существует.");
            }
        }

        if (request.GlobalCategoryId.HasValue)
        {
            var globalCategoryExists = await context.GlobalCategories
                .AnyAsync(c => c.Id == request.GlobalCategoryId.Value && c.IsActive, ct);
            if (!globalCategoryExists)
            {
                throw new ArgumentException("Global category was not found.");
            }
        }

        var isDuplicate = await categoryRepository.AnyAsync(c => 
            c.Name.ToLower() == request.Name.ToLower() && 
            c.ShopId == request.ShopId && 
            c.ParentCategoryId == request.ParentId, ct);

        if (isDuplicate)
        {
            throw new InvalidOperationException("Категория с таким именем уже существует в данном разделе.");
        }

        var category = new Category(request.Name, request.ShopId, request.ParentId, request.GlobalCategoryId);
        
        await categoryRepository.AddAsync(category);
        await categoryRepository.SaveAsync(ct);
        
        return category.Id;
    }
}
