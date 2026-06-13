using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    /// <summary>
    /// Запрос на создание бренда.
    /// </summary>
    /// <param name="Name">Название бренда. Должно быть уникальным.</param>
    /// <param name="LogoUrl">URL или путь к логотипу бренда.</param>
    public record CreateBrandCommand(string Name, string LogoUrl) : IRequest<Guid>;
    public class CreateBrandCommandHandler(IBrandRepository brandRepository) : IRequestHandler<CreateBrandCommand, Guid>
    {
        public async Task<Guid> Handle(CreateBrandCommand request, CancellationToken ct)
        {
            var existingBrand = await brandRepository.GetByNameAsync(request.Name);
            if (existingBrand != null)
                throw new ArgumentException("Бренд с таким именем уже существует.");

            var brand = new Brand(request.Name, request.LogoUrl);

            await brandRepository.AddAsync(brand);
            await brandRepository.SaveAsync(ct);

            return brand.Id;
        }
    }
}
