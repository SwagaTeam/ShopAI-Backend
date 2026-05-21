using AutoMapper;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record ShopsByCategoryVm(string CategoryName, List<ShopShortDto> Shops);

public record GetShopsByCategoryListQuery : IRequest<List<ShopsByCategoryVm>>;

public class GetShopsByCategoryListQueryHandler(
    ICategoryRepository categoryRepository,
    IMapper mapper) 
    : IRequestHandler<GetShopsByCategoryListQuery, List<ShopsByCategoryVm>>
{
    public async Task<List<ShopsByCategoryVm>> Handle(GetShopsByCategoryListQuery request, CancellationToken ct)
    {
        var categories = await categoryRepository.GetAllWithShopsAsync(ct);

        if (!categories.Any())
        {
            return new List<ShopsByCategoryVm>();
        }

        var result = categories
            .GroupBy(c => c.Name)
            .Select(g => new ShopsByCategoryVm(
                g.Key,
                mapper.Map<List<ShopShortDto>>(
                    g.Where(c => c.Shop != null)
                     .Select(c => c.Shop)
                     .DistinctBy(s => s!.Id)
                     .ToList()
                )
            ))
            .OrderBy(vm => vm.CategoryName)
            .ToList();

        return result;
    }
}