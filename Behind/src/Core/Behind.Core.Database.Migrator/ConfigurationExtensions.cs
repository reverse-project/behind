using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Core.Database.Migrator;

public static class ConfigurationExtensions
{
    extension(DatabaseConfiguration database)
    {
        public HostApplicationBuilder Register(HostApplicationBuilder builder) => database.RegisterSqlite(builder);

        HostApplicationBuilder RegisterSqlite(HostApplicationBuilder builder)
        {
            return database.Provider.Match(
                none: () => builder,
                some: provider =>
                {
                    if (provider is Common.Configuration.Sqlite)
                    {
                        builder.Services.AddDbContext<AbstractCoreContext, CoreContextSqlite>(options =>
                            database.Key.Match(
                                none: () => { },
                                some: key =>
                                {
                                    var connectionString = builder.Configuration.GetConnectionString(key);
                                    options.UseSqlite(connectionString);
                                }));
                        builder.Services.AddDbContext<VersioningContextSqlite>(options =>
                            database.Key.Match(
                                none: () => { },
                                some: key => options.UseVersioningSqlite(builder.Configuration.GetConnectionString(key))));
                    }

                    return builder;
                }
            );
        }
    }
}