using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Requests;

namespace ShopAI.Application.Handlers;

public class GetProductsByFiltersQuery : IRequest<PagedResult<ProductDetailsDto>>
{
    public GetProductsByFiltersRequest Filters { get; }

    public GetProductsByFiltersQuery(GetProductsByFiltersRequest filters)
    {
        Filters = filters;
    }
}

public class GetProductsByFiltersQueryHandler : IRequestHandler<GetProductsByFiltersQuery, PagedResult<ProductDetailsDto>>
{
    private readonly IProductRepository _productRepository;

    
    private readonly IProductDtoFactory _productDtoFactory;

    public GetProductsByFiltersQueryHandler(
        IProductRepository productRepository,
        ICartRepository cartRepository,
        IProductDtoFactory productDtoFactory)
    {
        _productRepository = productRepository;
        _productDtoFactory = productDtoFactory;
    }

    public async Task<PagedResult<ProductDetailsDto>> Handle(GetProductsByFiltersQuery request, CancellationToken cancellationToken)
    {
        var filters = request.Filters;

        var (products, totalCount) = await _productRepository.GetByFiltersAsync(filters, cancellationToken);
        
        var productDtos = new List<ProductDetailsDto>();

        foreach (var product in products)
        {
            var productDto = await _productDtoFactory.CreateDetailsDtoAsync(product, cancellationToken);
            productDtos.Add(productDto);
        }

        return new PagedResult<ProductDetailsDto>
        {
            Items = productDtos,
            TotalCount = totalCount,
            PageNumber = filters.PageNumber,
            PageSize = filters.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filters.PageSize)
        };
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}