using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ShopAI.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
        });
        
        services.AddAutoMapper(_ => {}, assembly);
        return services;
    }
}