using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShopAI.Infrastructure.DependencyInjection
{
    public class DatabaseMigrator(IServiceProvider serviceProvider, ILogger<DatabaseMigrator> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            try
            {
                logger.LogInformation("Checking and applying database migrations...");
                await context.Database.MigrateAsync(cancellationToken);
                await SeedAdminAsync(context, configuration, cancellationToken);
                await SeedGlobalCategoriesAsync(context, cancellationToken);
                logger.LogInformation("Migrations applied successfully.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Critical database migration error.");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task SeedAdminAsync(AppDbContext context, IConfiguration configuration, CancellationToken cancellationToken)
        {
            var adminEmail = configuration["Admin:Email"];
            var adminPassword = configuration["Admin:Password"];
            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                return;
            }

            var normalizedEmail = adminEmail.Trim().ToLowerInvariant();
            var admin = await context.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (admin == null)
            {
                var (hash, salt) = HashPassword(adminPassword);
                admin = new User
                {
                    Email = normalizedEmail,
                    Role = User.AdminRole,
                    Password = hash,
                    Salt = salt,
                    FullName = configuration["Admin:FullName"] ?? "Default Admin",
                    Phone = configuration["Admin:Phone"] ?? "+70000000001"
                };

                await context.Users.AddAsync(admin, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seed created admin user for {Email}", normalizedEmail);
                return;
            }

            if (admin.Role != User.AdminRole)
            {
                admin.Role = User.AdminRole;
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seed promoted existing user to Admin for {Email}", normalizedEmail);
            }
        }

        private async Task SeedGlobalCategoriesAsync(AppDbContext context, CancellationToken cancellationToken)
        {
            var defaults = new (string Name, string Slug, int SortOrder)[]
            {
                ("Электроника", "electronics", 10),
                ("Одежда", "clothing", 20),
                ("Дом и кухня", "home-kitchen", 30),
                ("Мебель", "furniture", 40),
                ("Спорт", "sport", 50),
                ("Красота и здоровье", "beauty-health", 60),
                ("Товары для взрослых", "adult", 70),
                ("Другое", "other", 100)
            };

            foreach (var item in defaults)
            {
                var exists = await context.GlobalCategories.AnyAsync(c => c.Slug == item.Slug, cancellationToken);
                if (exists) continue;

                context.GlobalCategories.Add(new GlobalCategory(item.Name, item.Slug, item.SortOrder));
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static (string Hash, string Salt) HashPassword(string password)
        {
            const int degreeOfParallelism = 8;
            const int memorySize = 65536;
            const int iterations = 4;
            const int hashLength = 32;
            const int saltLength = 16;

            var saltBytes = new byte[saltLength];
            RandomNumberGenerator.Fill(saltBytes);

            var passwordBytes = Encoding.UTF8.GetBytes(password);
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = saltBytes,
                DegreeOfParallelism = degreeOfParallelism,
                MemorySize = memorySize,
                Iterations = iterations
            };

            var hashBytes = argon2.GetBytes(hashLength);
            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }
    }
}
