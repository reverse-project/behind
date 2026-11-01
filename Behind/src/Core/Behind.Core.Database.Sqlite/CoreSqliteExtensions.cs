using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Core.Database.Sqlite;

public static class CoreSqliteExtensions
{
    extension(DbContextOptionsBuilder options)
    {
        public DbContextOptionsBuilder UseVersioningSqlite(string? connectionString) =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsHistoryTable(VersioningContextSqlite.MigrationsHistoryTable));
    }

    extension(DatabaseConfiguration database)
    {
        public WebApplicationBuilder RegisterSqlite(WebApplicationBuilder builder)
        {
            return database.Provider.Match(
                none: () => builder,
                some: provider =>
                {
                    if (provider is SqliteProvider)
                    {
                        builder.Services.TryAddSingleton(TimeProvider.System);
                        builder.Services.TryAddScoped<IAuditActorProvider, AutonomousAuditActorProvider>();
                        builder.Services.AddScoped<Func<IAuditActorProvider>>(services =>
                            services.GetRequiredService<IAuditActorProvider>);
                        builder.Services.AddScoped<AuditSaveChangesInterceptor>();
                        builder.Services.AddDbContext<CoreContextSqlite>((services, options) =>
                        {
                            options.AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>());
                            database.Key.Match(
                                none: () => { },
                                some: key =>
                                {
                                    var connectionString = builder.Configuration.GetConnectionString(key);
                                    options.UseSqlite(connectionString);
                                });
                        });
                        builder.Services.AddDbContext<VersioningContextSqlite>((services, options) =>
                        {
                            options.AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>());
                            database.Key.Match(
                                none: () => { },
                                some: key => options.UseVersioningSqlite(builder.Configuration.GetConnectionString(key)));
                        });
                        builder.Services.AddScoped<AbstractVersioningContext>(services =>
                            services.GetRequiredService<VersioningContextSqlite>());
                        builder.Services.AddScoped<AbstractCoreContext>(services => services.GetRequiredService<CoreContextSqlite>());
                        builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<CoreContextSqlite>());
                    }

                    return builder;
                });
        }
    }
}