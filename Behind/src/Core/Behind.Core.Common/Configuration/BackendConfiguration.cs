namespace Behind.Core.Common.Configuration;

public sealed record BackendConfiguration
{
    public const string SectionName = "Backend";
    public const string VfsDirectoryName = "vfs";

    public int AuditSearchMaxEntries { get; init; } = 10000;

    public int AuditSearchMaxCandidates { get; init; } = 10000;

    public string? DataDirectory { get; init; }

    public string VfsRoot =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? throw new InvalidOperationException(
                $"'{SectionName}:{nameof(DataDirectory)}' must be set in backend.json.")
            : Path.Combine(DataDirectory, VfsDirectoryName);
}
