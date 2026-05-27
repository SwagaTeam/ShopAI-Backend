using System.Reflection;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Application.Helpers.Implementations;
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

        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddHttpClient<IOpenRouterClient, OpenRouterClient>((sp, client) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            client.BaseAddress = new Uri(cfg["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        return services;
    }
}
