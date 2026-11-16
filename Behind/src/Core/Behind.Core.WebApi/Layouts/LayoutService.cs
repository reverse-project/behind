using System.Text.Json;
using Behind.Common.Modules;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Layouts;

public sealed class LayoutService(IOptions<BackendConfiguration> backend, ModuleCatalog modules) : ILayoutService, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private string FilePath => string.IsNullOrWhiteSpace(backend.Value.DataDirectory)
        ? throw new InvalidOperationException("Backend:DataDirectory must be configured.")
        : Path.Combine(backend.Value.DataDirectory, "layouts.json");

    public async Task<LayoutConfiguration> Read(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            return await ReadFile(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LayoutSaveResult> Save(
        LayoutConfiguration configuration,
        bool manageDeleted,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            var current = await ReadFile(cancellationToken);

            if (configuration.Revision != current.Revision)
            {
                return new LayoutSaveResult("conflict", null);
            }

            if (!configuration.IsValid(modules))
            {
                return new LayoutSaveResult("invalid", null);
            }

            var incoming = configuration.Layouts.ToDictionary(layout => layout.Id);
            var hidden = current.Layouts.Where(layout => layout.Deleted).ToArray();

            if (!manageDeleted && hidden.Any(layout => incoming.ContainsKey(layout.Id)))
            {
                return new LayoutSaveResult("forbidden", null);
            }

            if (current.Layouts.Any(layout => !layout.Deleted && !incoming.ContainsKey(layout.Id)))
            {
                return new LayoutSaveResult("invalid", null);
            }

            var sanitized = configuration.Sanitize();
            var saved = sanitized with
            {
                SchemaVersion = 1,
                Revision = checked(current.Revision + 1),
                Layouts = manageDeleted
                    ? sanitized.Layouts
                    : [..sanitized.Layouts, ..hidden]
            };
            if (!saved.IsValid(modules, requireInstalledWidgets: false))
            {
                return new LayoutSaveResult("invalid", null);
            }
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(saved, JsonOptions), cancellationToken);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }

            return new LayoutSaveResult(null, saved);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<LayoutConfiguration> ReadFile(CancellationToken cancellationToken)
    {
        var path = FilePath;

        if (!File.Exists(path))
        {
            return new LayoutConfiguration();
        }

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var configuration = document.RootElement.Deserialize<LayoutConfiguration>(JsonOptions);

        if (configuration is not null && !document.RootElement.EnumerateObject()
            .Any(property => property.Name.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase)))
        {
            configuration = configuration with { SchemaVersion = 0 };
        }

        if (configuration is null || !configuration.IsValid(modules, requireInstalledWidgets: false))
        {
            throw new InvalidDataException("The layout configuration is invalid.");
        }

        return configuration.UpgradePageLayouts().Sanitize();
    }

    public void Dispose() => gate.Dispose();
}
