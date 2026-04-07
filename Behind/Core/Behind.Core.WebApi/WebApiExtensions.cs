using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi;

public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddCore()
        {
            builder.Services.AddOpenApi();
            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseCore()
        {
            application.MapOpenApi();
            
            application.MapGet("/hello", () => "Hello World!");
            
            return application;
        }
    }
}