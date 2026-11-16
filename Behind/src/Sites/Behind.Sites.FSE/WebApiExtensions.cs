using System.Diagnostics.CodeAnalysis;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi;
using Behind.Sites.FSE.Database.Postgres;
using Behind.Sites.FSE.Database.Sqlite;
using Behind.Sport.WebApi;
using Scalar.AspNetCore;

namespace Behind.Sites.FSE;

[ExcludeFromCodeCoverage]
public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddFse() => builder
            .AddSerilog()
            .AddOpenTelemetry()
            .AddCore()
            .AddFseDatabase()
            .AddSport();

        WebApplicationBuilder AddFseDatabase()
        {
            var database = builder.Configuration
                .GetSection(DatabaseConfiguration.SectionName)
                .GetOptionAware<DatabaseConfiguration>() ?? new DatabaseConfiguration();
            database.RegisterSqlite<FseContextSqlite>(builder);
            database.RegisterPostgres<FseContextPostgres>(builder);

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseFse()
        {
            application.UseCore();
            application.UseSport();
            application.MapScalarApiReference();

            return application;
        }
    }
}
