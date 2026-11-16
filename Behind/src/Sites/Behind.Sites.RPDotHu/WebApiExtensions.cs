using System.Diagnostics.CodeAnalysis;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Core.WebApi;
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
            .AddCore();
    }

    extension(WebApplication application)
    {
        public WebApplication UseRpDotHu()
        {
            application.UseCore();
            application.MapScalarApiReference();

            return application;
        }
    }
}
