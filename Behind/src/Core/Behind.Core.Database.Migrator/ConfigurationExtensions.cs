using Behind.Core.Common.Configuration;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Microsoft.Extensions.Hosting;

namespace Behind.Core.Database.Migrator;

public static class ConfigurationExtensions
{
    extension(DatabaseConfiguration database)
    {
        public HostApplicationBuilder Register(HostApplicationBuilder builder)
        {
            database.RegisterSqlite(builder);
            database.RegisterPostgres(builder);

            return builder;
        }
    }
}
