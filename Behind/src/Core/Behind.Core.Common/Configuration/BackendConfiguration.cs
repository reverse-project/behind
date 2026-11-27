using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Behind.Core.Common.Configuration;

public sealed record BackendConfiguration
{
    public const string SectionName = "Backend";
    public const string VfsDirectoryName = "vfs";

    public int AuditSearchMaxEntries { get; init; } = 10000;

    public int AuditSearchMaxCandidates { get; init; } = 10000;

    public IReadOnlyDictionary<string, bool> Modules { get; init; } = new Dictionary<string, bool>();

    public static BackendConfiguration Read(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(SectionName);
        var defaults = new BackendConfiguration();
        int ReadBudget(string property, int fallback) =>
            int.TryParse(section?[property], CultureInfo.InvariantCulture, out var value) ? Math.Max(1, value) : fallback;

        return defaults with
        {
            DataDirectory = section?[nameof(DataDirectory)],
            Modules = section?.GetSection(nameof(Modules)).Get<Dictionary<string, bool>>() ?? new Dictionary<string, bool>(),
            AuditSearchMaxEntries = ReadBudget(nameof(AuditSearchMaxEntries), defaults.AuditSearchMaxEntries),
            AuditSearchMaxCandidates = ReadBudget(nameof(AuditSearchMaxCandidates), defaults.AuditSearchMaxCandidates)
        };
    }

    public string? DataDirectory { get; init; }

    public string VfsRoot =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? throw new InvalidOperationException(
                $"'{SectionName}:{nameof(DataDirectory)}' must be set in backend.json.")
            : Path.Combine(DataDirectory, VfsDirectoryName);
}
