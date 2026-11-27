using Behind.Common.Modules;
using Behind.Common.WebApi.Middleware;
using Behind.Sport.Database;
using Behind.Sport.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Sport.WebApi;

public static class SportWebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddSport()
        {
            builder.Services
                .AddModule(SportModule.Descriptor)
                .AddSportDatabase();

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseSport()
        {
            application.MapGroup("").WithMetadata(new ModuleEndpoint(SportModule.Descriptor.Key))
                .MapSportEndpoints();

            return application;
        }
    }
}
