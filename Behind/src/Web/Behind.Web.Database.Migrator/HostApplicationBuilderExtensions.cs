using Behind.Common.Modules;
using Behind.Core.Database.Migrator;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Quotes.Database;
using Behind.Quotes.Database.Postgres;
using Behind.Quotes.Database.Sqlite;
using Behind.Quotes.WebApi;
using Behind.Web.Database.Postgres;
using Behind.Web.Database.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Web.Database.Migrator;

public static class HostApplicationBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder AddWebMigrator()
        {
            builder.Services
                .AddModule(QuotesModule.Descriptor)
                .AddQuotesDatabase();

            return builder.AddMigrator((database, host) =>
            {
                database.Register<WebContextSqlite, WebContextPostgres>(host);
                database.RegisterSqliteModule<QuotesContextSqlite>(host, QuotesModule.Descriptor);
                database.RegisterPostgresModule<QuotesContextPostgres>(host, QuotesModule.Descriptor);

                return host;
            });
        }
    }
}
