using Behind.Common.WebApi.Handlers;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi;

public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddCore()
        {
            builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
            builder.Services.AddOpenApi();
            builder.Services.AddTransient<IAuditLogEntryQueries, AuditLogEntryQueries>();
            builder.Services.AddDbContext<AbstractCoreContext, CoreContextSqlite>(
                options => options.UseSqlite("Data Source=/workspace/Data/core.db")
            );
            
            builder.Configuration.AddJsonFile("security.json", true, true);
            builder.Services.Configure<SecurityConfiguration>(
                builder.Configuration.GetSection(SecurityConfiguration.SectionName)
            );
            
            builder.Configuration.AddJsonFile("frontend.json", true, true);
            builder.Services.Configure<FrontendConfiguration>(
                builder.Configuration.GetSection(FrontendConfiguration.SectionName)
            );
            
            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseCore()
        {
            application.UseExceptionHandler(_ => { });
            application.MapOpenApi();
            
            application.MapGet("/config", ConfigEndpoint.ExecuteGet);
            application.MapGet("/auditLog", AuditLogEntryEndpoints.Get)
                .Produces<IEnumerable<AuditLogEntry>>()
                .Produces(204)
                .Produces(400)
                .Produces(500);
            
            return application;
        }
    }
}