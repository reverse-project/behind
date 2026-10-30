using System.Diagnostics.CodeAnalysis;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Core.WebApi;
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
            .AddCore();
    }
    
    extension(WebApplication application)
    {
        public WebApplication UseWeb()
        {
            application.UseCore();
            application.MapScalarApiReference();
            
            return application;
        }
    }
}