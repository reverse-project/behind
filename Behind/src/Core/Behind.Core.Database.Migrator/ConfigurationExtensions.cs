using Behind.Core.Common.Configuration;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Microsoft.Extensions.Hosting;

namespace Behind.Core.Database.Migrator;

public static class ConfigurationExtensions
{
    extension(DatabaseConfiguration database)
    {
        public HostApplicationBuilder Register(HostApplicationBuilder builder) =>
            database.Register<CoreContextSqlite, CoreContextPostgres>(builder);

        public HostApplicationBuilder Register<TSqlite, TPostgres>(HostApplicationBuilder builder)
            where TSqlite : AbstractCoreContext
            where TPostgres : AbstractCoreContext
        {
            database.RegisterSqlite<TSqlite>(builder);
            database.RegisterPostgres<TPostgres>(builder);

            return builder;
        }
    }
}
