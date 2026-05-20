using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record UpdateBrandCommand(Guid Id, string Name, string LogoUrl) : IRequest<Unit>;
    public class UpdateBrandHandler(IBrandRepository brandRepository) : IRequestHandler<UpdateBrandCommand, Unit>
    {
        public async Task<Unit> Handle(UpdateBrandCommand request, CancellationToken ct)
        {
            var brand = await brandRepository.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("Бренд не найден.");

            if (brand.Name != request.Name)
            {
                var existingBrand = await brandRepository.GetByNameAsync(request.Name);
                if (existingBrand != null && existingBrand.Id != request.Id)
                    throw new ArgumentException("Бренд с таким именем уже существует.");
            }

            brand.Name = request.Name;
            brand.LogoUrl = request.LogoUrl;

            brandRepository.Update(brand);
            await brandRepository.SaveAsync(ct);

            return Unit.Value;
        }
    }
}
