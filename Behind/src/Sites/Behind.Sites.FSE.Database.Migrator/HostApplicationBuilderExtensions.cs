using Behind.Common.Modules;
using Behind.Core.Database.Migrator;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Sites.FSE.Database.Postgres;
using Behind.Sites.FSE.Database.Sqlite;
using Behind.Sport.Database;
using Behind.Sport.Database.Postgres;
using Behind.Sport.Database.Sqlite;
using Behind.Sport.WebApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Sites.FSE.Database.Migrator;

public static class HostApplicationBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder AddFseMigrator()
        {
            builder.Services
                .AddModule(SportModule.Descriptor)
                .AddSportDatabase();

            return builder.AddMigrator((database, host) =>
            {
                database.Register<FseContextSqlite, FseContextPostgres>(host);
                database.RegisterSqliteModule<SportContextSqlite>(host, SportModule.Descriptor);
                database.RegisterPostgresModule<SportContextPostgres>(host, SportModule.Descriptor);

                return host;
            });
        }
    }
}
