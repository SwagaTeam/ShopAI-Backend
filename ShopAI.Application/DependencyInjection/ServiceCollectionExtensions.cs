using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Security;

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

        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        return services;
    }
}