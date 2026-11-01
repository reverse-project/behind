using Behind.Common.Logging;
using Behind.Core.Database.Sqlite;
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
        var sqlite = scope.ServiceProvider.GetRequiredService<CoreContextSqlite>();

        var pending = (await sqlite.Database.GetPendingMigrationsAsync(stoppingToken)).ToList();
        logger.LogInformation(
            "Applying {PendingMigrationCount} pending migrations: {PendingMigrations}",
            () => [pending.Count, pending]);

        await sqlite.Database.MigrateAsync(stoppingToken);
        logger.LogInformation("Database migrations applied");

        await sqlite.EnsureBuiltInUsersAsync(stoppingToken);
        logger.LogDebug("Built-in users ensured");

        await sqlite.EnsureBuiltInRolesAsync(stoppingToken);
        logger.LogDebug("Built-in roles ensured");

        await sqlite.EnsureUsersHaveUsersRoleAsync(stoppingToken);
        logger.LogDebug("Users role ensured for all users");

        var host = scope.ServiceProvider.GetRequiredService<IHost>();
        await host.StopAsync(stoppingToken);
    }
}