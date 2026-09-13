using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi;

public static class ConfigurationExtensions
{
    extension(DatabaseConfiguration database)
    {
        public WebApplicationBuilder Register(WebApplicationBuilder builder) => database.RegisterSqlite(builder);

        WebApplicationBuilder RegisterSqlite(WebApplicationBuilder builder)
        {
            return database.Provider.Match(
                none: () => builder,
                some: provider =>
                {
                    if (provider is Sqlite)
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