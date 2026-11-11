using Behind.Common.Modules;
using Behind.Core.Database.Migrator;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Core.Database;
using Behind.Common.Database.Modules;
using Behind.Forum.Database;
using Behind.Forum.Database.Sqlite;
using Behind.Forum.Database.Postgres;
using Behind.Forum.WebApi;
using Behind.MarkdownViewer.WebApi;
using Behind.Quotes.Database;
using Behind.Quotes.Database.Postgres;
using Behind.Quotes.Database.Sqlite;
using Behind.Quotes.WebApi;
using Behind.Sport.Database;
using Behind.Sport.Database.Postgres;
using Behind.Sport.Database.Sqlite;
using Behind.Sport.WebApi;
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
                .AddScoped<IModuleUsers, UserProfiles>()
                .AddModule(ForumModule.Descriptor)
                .AddForumDatabase()
                .AddModule(MarkdownViewerModule.Descriptor)
                .AddModule(QuotesModule.Descriptor)
                .AddQuotesDatabase()
                .AddModule(SportModule.Descriptor)
                .AddSportDatabase();

            return builder.AddMigrator((database, host) =>
            {
                database.Register<WebContextSqlite, WebContextPostgres>(host);
                database.RegisterSqliteModule<ForumContextSqlite>(host, ForumModule.Descriptor);
                database.RegisterPostgresModule<ForumContextPostgres>(host, ForumModule.Descriptor);
                database.RegisterSqliteModule<QuotesContextSqlite>(host, QuotesModule.Descriptor);
                database.RegisterPostgresModule<QuotesContextPostgres>(host, QuotesModule.Descriptor);
                database.RegisterSqliteModule<SportContextSqlite>(host, SportModule.Descriptor);
                database.RegisterPostgresModule<SportContextPostgres>(host, SportModule.Descriptor);

                return host;
            });
        }
    }
}
