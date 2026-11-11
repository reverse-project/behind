using System.Diagnostics.CodeAnalysis;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi;
using Behind.Forum.WebApi;
using Behind.MarkdownViewer.WebApi;
using Behind.Quotes.WebApi;
using Behind.Sport.WebApi;
using Behind.Web.Database.Postgres;
using Behind.Web.Database.Sqlite;
using Scalar.AspNetCore;

namespace Behind.Web;

[ExcludeFromCodeCoverage]
public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddWeb() => builder
            .AddSerilog()
            .AddOpenTelemetry()
            .AddCore()
            .AddWebDatabase()
            .AddQuotes()
            .AddSport()
            .AddForum()
            .AddMarkdownViewer();

        WebApplicationBuilder AddWebDatabase()
        {
            var database = builder.Configuration
                .GetSection(DatabaseConfiguration.SectionName)
                .GetOptionAware<DatabaseConfiguration>() ?? new DatabaseConfiguration();
            database.RegisterSqlite<WebContextSqlite>(builder);
            database.RegisterPostgres<WebContextPostgres>(builder);

            return builder;
        }
    }
    
    extension(WebApplication application)
    {
        public WebApplication UseWeb()
        {
            application.UseCore();
            application.UseQuotes();
            application.UseSport();
            application.UseForum();
            application.UseMarkdownViewer();
            application.MapScalarApiReference();
            
            return application;
        }
    }
}
