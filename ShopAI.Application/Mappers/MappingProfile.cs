using AutoMapper;
using Domain.Entities;
using ShopAI.Application.Models;

namespace ShopAI.Application.Mappers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Product, ProductShortDto>()
            .ForMember(d => d.ShopName, opt => opt.MapFrom(s => s.Shop.Name))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand != null ? s.Brand.Name : null));
    }
}