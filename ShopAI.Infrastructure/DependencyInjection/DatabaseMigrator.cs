using Domain.Entities;
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
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return;
            }

            var normalizedEmail = adminEmail.Trim().ToLowerInvariant();
            var admin = await context.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (admin != null && admin.Role != User.AdminRole)
            {
                admin.Role = User.AdminRole;
                logger.LogInformation("Seed promoted existing user to Admin for {Email}", normalizedEmail);
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (admin == null)
            {
                logger.LogWarning("Admin seed email {Email} not found in Users. Register this email first, then restart app.", normalizedEmail);
            }
        }
    }
}
