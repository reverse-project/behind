using Behind.Common.Modules;
using Behind.Core.Database.Migrator;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Sites.RPDotHu.Database.Postgres;
using Behind.Sites.RPDotHu.Database.Sqlite;
using Behind.Contact.Database;
using Behind.Contact.Database.Postgres;
using Behind.Contact.Database.Sqlite;
using Behind.Contact.WebApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Sites.RPDotHu.Database.Migrator;

public static class HostApplicationBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder AddRpDotHuMigrator()
        {
            builder.Services
                .AddModule(ContactModule.Descriptor)
                .AddContactDatabase();

            return builder.AddMigrator((database, host) =>
            {
                database.Register<RpDotHuContextSqlite, RpDotHuContextPostgres>(host);
                database.RegisterSqliteModule<ContactContextSqlite>(host, ContactModule.Descriptor);
                database.RegisterPostgresModule<ContactContextPostgres>(host, ContactModule.Descriptor);

                return host;
            });
        }
    }
}
