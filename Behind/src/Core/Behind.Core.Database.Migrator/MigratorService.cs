using Behind.Common.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.Database.Migrator;

public sealed class MigratorService(IServiceProvider services, ILogger<MigratorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>();
        var context = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();

        var pending = await migrator.PendingMigrationsAsync(stoppingToken);
        logger.LogInformation(
            "Applying {PendingMigrationCount} pending migrations: {PendingMigrations}",
            () => [pending.Count, pending]);

        await migrator.MigrateAsync(stoppingToken);
        logger.LogInformation("Database migrations applied");

        var versioning = scope.ServiceProvider.GetRequiredService<AbstractVersioningContext>();
        await versioning.Database.MigrateAsync(stoppingToken);
        logger.LogInformation("Versioning database migrations applied");

        await context.EnsureBuiltInUsersAsync(stoppingToken);
        logger.LogDebug("Built-in users ensured");

        await context.EnsureBuiltInRolesAsync(stoppingToken);
        logger.LogDebug("Built-in roles ensured");

        await context.EnsureUsersHaveUsersRoleAsync(stoppingToken);
        logger.LogDebug("Users role ensured for all users");

        await context.EnsureBuiltInMenusAsync(stoppingToken);
        logger.LogDebug("Built-in menus ensured");

        var host = scope.ServiceProvider.GetRequiredService<IHost>();
        await host.StopAsync(stoppingToken);
    }
}