using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using ShopAI.Infrastructure.Repositories.Abstractions;
using ShopAI.Infrastructure.Repositories.Implementations;
using ShopAI.Infrastructure.Security;
using ShopAI.Infrastructure.Storage;

namespace ShopAI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();

        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<IRecentlyViewedRepository, RecentlyViewedRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        services.AddSingleton<IMinioClient>(_ =>
        {
            var endpoint = configuration["Minio:Endpoint"] ?? "localhost:9000";
            var accessKey = configuration["Minio:AccessKey"] ?? "minioadmin";
            var secretKey = configuration["Minio:SecretKey"] ?? "minioadmin";
            var useSsl = bool.TryParse(configuration["Minio:UseSsl"], out var parsed) && parsed;

            var builder = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey);

            if (useSsl)
            {
                builder = builder.WithSSL();
            }

            return builder.Build();
        });

        services.AddHostedService<DatabaseMigrator>();

        return services;
    }
}
