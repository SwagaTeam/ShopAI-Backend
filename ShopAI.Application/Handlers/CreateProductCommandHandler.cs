using System.Text.Json;
using Domain.Entities;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на создание товара.
/// </summary>
/// <param name="ShopId">Идентификатор магазина, в котором создается товар.</param>
/// <param name="Name">Название товара. Должно быть уникальным внутри магазина.</param>
/// <param name="Price">Цена товара. Должна быть больше нуля.</param>
/// <param name="CategoryId">Идентификатор категории товара. Категория должна принадлежать указанному магазину.</param>
/// <param name="Description">Описание товара.</param>
/// <param name="ImageUrl">Путь или URL основного изображения товара.</param>
/// <param name="StockQuantity">Количество товара на складе. Не может быть отрицательным.</param>
/// <param name="BrandId">Необязательный идентификатор бренда товара.</param>
/// <param name="Tags">Необязательный список тегов товара.</param>
/// <param name="Attributes">Необязательные характеристики товара в формате ключ-значение.</param>
public record CreateProductCommand(
    Guid ShopId,
    string Name,
    decimal Price,
    Guid CategoryId,
    string Description,
    string ImageUrl,
    int StockQuantity,
    Guid? BrandId,
    List<string>? Tags,
    Dictionary<string, string>? Attributes) : IRequest<Guid>;

public class CreateProductCommandHandler(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IShopRepository shopRepository,
    IBrandRepository brandRepository)
    : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken ct)
    {
        if (!await shopRepository.ExistsAsync(request.ShopId, ct))
        {
            throw new ArgumentException($"Магазин с ID {request.ShopId} не найден.");
        }

        var categoryExists = await categoryRepository.ExistsAsync(request.CategoryId, ct);
        if (!categoryExists)
        {
            throw new ArgumentException($"Категория с ID {request.CategoryId} не существует.");
        }

        var categories = await categoryRepository.GetByShopIdAsync(request.ShopId);
        if (categories.All(c => c.Id != request.CategoryId))
        {
            throw new InvalidOperationException("Указанная категория не принадлежит данному магазину.");
        }

        if (request.BrandId.HasValue)
        {
            var brandExists = await brandRepository.ExistsAsync(request.BrandId.Value, ct);
            if (!brandExists)
            {
                throw new ArgumentException("Указанный бренд не найден.");
            }
        }

        if (request.Price <= 0)
        {
            throw new ArgumentException("Цена товара должна быть больше нуля.");
        }

        if (request.StockQuantity < 0)
        {
            throw new ArgumentException("Количество товара на складе не может быть отрицательным.");
        }

        var isDuplicate = await productRepository.AnyAsync(p =>
            p.ShopId == request.ShopId &&
            p.Name.ToLower() == request.Name.ToLower(), ct);

        if (isDuplicate)
        {
            throw new InvalidOperationException("Товар с таким названием уже существует в этом магазине.");
        }

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

        var tags = (request.Tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        product.Tags = string.Join(",", tags);
        product.AttributesJson = JsonSerializer.Serialize(request.Attributes ?? new Dictionary<string, string>());

        await productRepository.AddAsync(product);
        await productRepository.SaveAsync(ct);

        return product.Id;
    }
}
