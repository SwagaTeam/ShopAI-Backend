using AutoMapper;
using Domain.Entities;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record MainPageProductsVm(List<ProductShortDto> Latest, List<ProductShortDto> Popular);
public record GetMainPageProductsQuery(int Count) : IRequest<MainPageProductsVm>;

public class GetMainPageProductsQueryHandler(
    IProductRepository productRepository, 
    IMapper mapper) 
    : IRequestHandler<GetMainPageProductsQuery, MainPageProductsVm>
{
    public async Task<MainPageProductsVm> Handle(GetMainPageProductsQuery request, CancellationToken ct)
    {
        var count = request.Count switch
        {
            <= 0 => 10,     
            > 100 => 100,    
            _ => request.Count
        };

        var latestEntities = await productRepository.GetLatestAsync(count);
        var popularEntities = await productRepository.GetPopularAsync(count);

        return new MainPageProductsVm(
            mapper.Map<List<ProductShortDto>>(latestEntities),
            mapper.Map<List<ProductShortDto>>(popularEntities)
        );
    }
}