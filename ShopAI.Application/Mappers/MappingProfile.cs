using AutoMapper;
using Domain.Entities;
using ShopAI.Application.Models;

namespace ShopAI.Application.Mappers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Маппинг для списков товаров
        CreateMap<Product, ProductShortDto>()
            .ForMember(d => d.ShopName, opt => opt.MapFrom(s => s.Shop.Name))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand != null ? s.Brand.Name : null));

        // Детальный маппинг для одного товара
        CreateMap<Product, ProductDetailsDto>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category.Name))
            .ForMember(d => d.ShopName, opt => opt.MapFrom(s => s.Shop.Name))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand != null ? s.Brand.Name : null));

        // Маппинг для отзывов
        CreateMap<ProductReview, ProductReviewDto>()
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User.FullName))
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.UserId, opt => opt.MapFrom(s => s.UserId))
            .ForMember(d => d.Rating, opt => opt.MapFrom(s => s.Rating))
            .ForMember(d => d.Comment, opt => opt.MapFrom(s => s.Comment))
            .ForMember(d => d.ImagePaths, opt => opt.MapFrom(s => s.ImagePaths))
            .ForMember(d => d.CreatedAtUtc, opt => opt.MapFrom(s => s.CreatedAtUtc));
        
        // Маппинг для полной модели магазина
        CreateMap<Shop, ShopDto>()
            .ForMember(d => d.OwnerName, opt => opt.MapFrom(s => s.Owner != null ? s.Owner.FullName : "Неизвестный владелец"));

        // Маппинг для укороченной модели магазина
        CreateMap<Shop, ShopShortDto>();

        CreateMap<Category, CategoryDto>();

        CreateMap<Brand, BrandDto>();
    }
}