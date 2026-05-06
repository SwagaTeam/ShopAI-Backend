using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record ShopsByCategoryVm(string CategoryName, List<ShopShortDto> Shops);

public record GetShopsByCategoryListQuery : IRequest<List<ShopsByCategoryVm>>;

public class GetShopsByCategoryListHandler(
    ICategoryRepository categoryRepository) 
    : IRequestHandler<GetShopsByCategoryListQuery, List<ShopsByCategoryVm>>
{
    public async Task<List<ShopsByCategoryVm>> Handle(GetShopsByCategoryListQuery request, CancellationToken ct)
    {
        var categories = await categoryRepository.GetAllWithShopsAsync(ct);

        if (categories == null || !categories.Any())
        {
            return new List<ShopsByCategoryVm>();
        }

        var result = categories
            .GroupBy(c => c.Name)
            .Select(g => new ShopsByCategoryVm(
                g.Key,
                g.Where(c => c.Shop != null)
                    .Select(c => new ShopShortDto(
                        c.ShopId, 
                        c.Shop!.Name, 
                        c.Shop.UrlAlias))
                    .DistinctBy(s => s.Id) 
                    .ToList()
            ))
            .OrderBy(vm => vm.CategoryName) 
            .ToList();

        return result;
    }
}