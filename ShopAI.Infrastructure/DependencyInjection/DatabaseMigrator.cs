using Microsoft.EntityFrameworkCore;
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

            try
            {
                logger.LogInformation("Проверка и запуск миграций базы данных...");
                await context.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Миграции успешно применены.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Критическая ошибка при миграции БД!");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
