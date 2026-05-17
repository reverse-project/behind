using OpenTelemetry;
using OpenTelemetry.Exporter;

namespace Behind.Common.Configuration;

public sealed record OpenTelemetryConfiguration(
    string ServiceName,
    bool IsEnabled,
    OpenTelemetryConfiguration.OtlpExporterConfiguration? Traces,
    OpenTelemetryConfiguration.OtlpExporterConfiguration? Metrics,
    OpenTelemetryConfiguration.LoggingConfiguration? Logging
)
{
    public const string SectionName = "OpenTelemetry";

    public record OtlpExporterConfiguration(
        bool IsEnabled,
        Uri? Endpoint,
        OtlpExportProtocol? Protocol,
        IEnumerable<Instrumentation>? Instruments
    );

    public record LoggingConfiguration(
        bool IsEnabled,
        Uri? Endpoint,
        OtlpExportProtocol? Protocol,
        IEnumerable<Instrumentation>? Instruments,
        bool? IncludeFormattedMessage,
        bool? IncludeScopes,
        bool? ParseStateValues
    );

    public abstract record Instrumentation
    {
        public static readonly Instrumentation AspNetCoreInstrumentation =
            (NamedInstrumentation)nameof(AspNetCoreInstrumentation);

        public static readonly Instrumentation HttpClientInstrumentation =
            (NamedInstrumentation)nameof(HttpClientInstrumentation);

        public static readonly Instrumentation RuntimeInstrumentation =
            (NamedInstrumentation)nameof(RuntimeInstrumentation);

        public static readonly Instrumentation OtlpExporter =
            (NamedInstrumentation)nameof(OtlpExporter);

        sealed record NamedInstrumentation(string Name) : Instrumentation
        {
            public static implicit operator string(NamedInstrumentation self) => self.Name;
            public static implicit operator NamedInstrumentation(string name) => new(name);
        }
    }
}

public static class OpenTelemetryConfigurationExtensions
{
    extension(OpenTelemetryConfiguration? configuration)
    {
        public void ApplyIfEnabled(Action<OpenTelemetryConfiguration, OpenTelemetryBuilder> action, OpenTelemetryBuilder builder)
        {
            if (configuration is not null && configuration.IsEnabled)
            {
                action(configuration, builder);
            }
        }

        public OpenTelemetryConfiguration? ApplyTraces(
            Action<OpenTelemetryConfiguration.OtlpExporterConfiguration,
                List<OpenTelemetryConfiguration.Instrumentation>> action)
        {
            if (configuration is not null && configuration.Traces?.IsEnabled == true)
            {
                action(configuration.Traces, configuration.Traces.Instruments?.Distinct().ToList() ?? []);
            }

            return configuration;
        }

        public OpenTelemetryConfiguration? ApplyMetrics(
            Action<OpenTelemetryConfiguration.OtlpExporterConfiguration,
                List<OpenTelemetryConfiguration.Instrumentation>> action)
        {
            if (configuration is not null && configuration.Metrics?.IsEnabled == true)
            {
                action(configuration.Metrics, configuration.Metrics.Instruments?.Distinct().ToList() ?? []);
            }

            return configuration;
        }

        public OpenTelemetryConfiguration? ApplyLogging(
            Action<OpenTelemetryConfiguration.LoggingConfiguration, List<OpenTelemetryConfiguration.Instrumentation>>
                action)
        {
            if (configuration is not null && configuration.Logging?.IsEnabled == true)
            {
                action(configuration.Logging, configuration.Logging.Instruments?.Distinct().ToList() ?? []);
            }

            return configuration;
        }
    }

    extension(OpenTelemetryConfiguration.OtlpExporterConfiguration oec)
    {
        public void Apply(
            Action<OpenTelemetryConfiguration.OtlpExporterConfiguration> action
        )
        {
            if (oec.IsEnabled)
            {
                action(oec);
            }
        }
    }

    extension(OpenTelemetryConfiguration.LoggingConfiguration lc)
    {
        public void Apply(
            Action<OpenTelemetryConfiguration.LoggingConfiguration> action
        )
        {
            if (lc.IsEnabled)
            {
                action(lc);
            }
        }

        public OpenTelemetryConfiguration.LoggingConfiguration Apply<T>(T value, Action<T> action)
        {
            lc.NotNull(_ => action(value));

            return lc;
        }
    }

    extension<T>(T instrumentation) where T : OpenTelemetryConfiguration.Instrumentation?
    {
        public  void Apply(
            IEnumerable<T> instruments,
            Action action
        )
        {
            if (instruments.Contains(instrumentation))
            {
                action();
            }
        }
    }

    extension<T>(T? instrumentation) where T : OpenTelemetryConfiguration.Instrumentation
    {
        public OpenTelemetryConfiguration.Instrumentation? ApplyChain(
            IEnumerable<T> instruments,
            Action action,
            OpenTelemetryConfiguration.Instrumentation? next = null
        )
        {
            OpenTelemetryConfiguration.Instrumentation? result = null;
            instrumentation.Apply(instruments, action);
            next.NotNull(x => result = x);
            return result;
        }
    }
}