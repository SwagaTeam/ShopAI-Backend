using AutoMapper;
using Domain.Entities;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record MainPageProductsVm(List<ProductShortDto> Latest, List<ProductShortDto> Popular);
public record GetMainPageProductsQuery(int Count) : IRequest<MainPageProductsVm>;

public class GetMainPageProductsHandler(
    IProductRepository productRepository, 
    IMapper mapper) 
    : IRequestHandler<GetMainPageProductsQuery, MainPageProductsVm>
{
    public async Task<MainPageProductsVm> Handle(GetMainPageProductsQuery request, CancellationToken ct)
    {
        var latestEntities = await productRepository.GetLatestAsync(request.Count);
        var popularEntities = await productRepository.GetPopularAsync(request.Count);

        var latestDtos = mapper.Map<List<ProductShortDto>>(latestEntities);
        var popularDtos = mapper.Map<List<ProductShortDto>>(popularEntities);

        return new MainPageProductsVm(latestDtos, popularDtos);
    }
}