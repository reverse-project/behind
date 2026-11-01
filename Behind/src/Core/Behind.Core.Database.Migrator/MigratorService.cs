using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Core.Database.Migrator;

public sealed class MigratorService(IServiceProvider services) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = services.CreateScope();
        var sqlite = scope.ServiceProvider.GetRequiredService<CoreContextSqlite>();
        await sqlite.Database.MigrateAsync(stoppingToken);
        await sqlite.EnsureBuiltInUsersAsync(stoppingToken);
        await sqlite.EnsureBuiltInRolesAsync(stoppingToken);
        await sqlite.EnsureUsersHaveUsersRoleAsync(stoppingToken);

        var host = scope.ServiceProvider.GetRequiredService<IHost>();
        await host.StopAsync(stoppingToken);
    }
}