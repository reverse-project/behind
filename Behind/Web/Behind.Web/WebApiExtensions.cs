using Behind.Common.WebApi;
using Behind.Core.WebApi;
using Scalar.AspNetCore;

namespace Behind.Web;

public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddWebApi() => builder
            .AddOpenTelemetry()
            .AddCore();
    }
    
    extension(WebApplication application)
    {
        public WebApplication UseWeb()
        {
            application.MapScalarApiReference();
            
            return application;
        }
    }
}