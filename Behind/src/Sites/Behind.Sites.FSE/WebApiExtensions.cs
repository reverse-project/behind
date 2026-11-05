using System.Diagnostics.CodeAnalysis;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Core.WebApi;
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
            .AddCore();
    }

    extension(WebApplication application)
    {
        public WebApplication UseFse()
        {
            application.UseCore();
            application.MapScalarApiReference();

            return application;
        }
    }
}
