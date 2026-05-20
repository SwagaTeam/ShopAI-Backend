using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record DeleteBrandCommand(Guid Id) : IRequest<Unit>;
    public class DeleteBrandHandler(IBrandRepository brandRepository) : IRequestHandler<DeleteBrandCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteBrandCommand request, CancellationToken ct)
        {
            var brand = await brandRepository.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("Бренд не найден.");

            // TODO: В будущем здесь можно добавить проверку на наличие товаров с этим брендом
            // var hasProducts = await productRepository.AnyAsync(p => p.BrandId == request.Id, ct);
            // if (hasProducts) throw new InvalidOperationException("Нельзя удалить бренд, к которому привязаны товары.");

            brandRepository.Delete(brand);
            await brandRepository.SaveAsync(ct);

            return Unit.Value;
        }
    }
}
