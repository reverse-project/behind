using System.Diagnostics.CodeAnalysis;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Contact.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi;
using Behind.Sites.RPDotHu.Database.Postgres;
using Behind.Sites.RPDotHu.Database.Sqlite;
using Scalar.AspNetCore;

namespace Behind.Sites.RPDotHu;

[ExcludeFromCodeCoverage]
public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddRpDotHu() => builder
            .AddSerilog()
            .AddOpenTelemetry()
            .AddCore()
            .AddRpDotHuDatabase()
            .AddContact();

        WebApplicationBuilder AddRpDotHuDatabase()
        {
            var database = builder.Configuration
                .GetSection(DatabaseConfiguration.SectionName)
                .GetOptionAware<DatabaseConfiguration>() ?? new DatabaseConfiguration();
            database.RegisterSqlite<RpDotHuContextSqlite>(builder);
            database.RegisterPostgres<RpDotHuContextPostgres>(builder);

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseRpDotHu()
        {
            application.UseCore();
            application.UseContact();
            application.MapScalarApiReference();

            return application;
        }
    }
}
