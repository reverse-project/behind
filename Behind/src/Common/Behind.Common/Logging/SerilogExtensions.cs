using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Behind.Common.Logging;

public static class SerilogExtensions
{
    public const string DefaultsResourceName = "Behind.Common.Logging.serilog.defaults.json";

    public const string CorrelationIdProperty = "CorrelationId";

    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        public TBuilder AddSerilog()
        {
            builder.Configuration.Sources.Insert(0, DefaultsSource());
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(
                (services, logger) => logger
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.With<CorrelationIdEnricher>(),
                writeToProviders: true);

            return builder;
        }
    }

    private static JsonStreamConfigurationSource DefaultsSource() =>
        new()
        {
            Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultsResourceName)
                     ?? throw new InvalidOperationException($"Embedded resource '{DefaultsResourceName}' not found.")
        };
}
