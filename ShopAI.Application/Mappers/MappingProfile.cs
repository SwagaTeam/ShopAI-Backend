using AutoMapper;
using Domain.Entities;
using ShopAI.Application.Models;

namespace ShopAI.Application.Mappers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {

        // Маппинг для товаров
        CreateMap<Product, ProductShortDto>()
            .ForMember(d => d.ShopName, opt => opt.MapFrom(s => s.Shop.Name))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand != null ? s.Brand.Name : null));

        // Маппинг для отзывов
        CreateMap<ProductReview, ProductReviewDto>()
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.FullName : "Аноним"));

        // Детальный маппинг для одного товара
        CreateMap<Product, ProductDetailsDto>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category.Name))
            .ForMember(d => d.ShopName, opt => opt.MapFrom(s => s.Shop.Name))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand != null ? s.Brand.Name : null));

        CreateMap<Shop, ShopDto>()
            .ForMember(d => d.OwnerName, opt => opt.MapFrom(s => s.Owner != null ? s.Owner.FullName : "Неизвестно"));

        CreateMap<Shop, ShopShortDto>();
    }
}