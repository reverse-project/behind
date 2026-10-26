using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Core.Database.Sqlite;

public static class CoreSqliteExtensions
{
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
                        builder.Services.AddDbContext<CoreContextSqlite>(options =>
                            database.Key.Match(
                                none: () => { },
                                some: key =>
                                {
                                    var connectionString = builder.Configuration.GetConnectionString(key);
                                    options.UseSqlite(connectionString);
                                }));
                        builder.Services.AddScoped<AbstractCoreContext>(services => services.GetRequiredService<CoreContextSqlite>());
                        builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<CoreContextSqlite>());
                    }

                    return builder;
                });
        }
    }
}