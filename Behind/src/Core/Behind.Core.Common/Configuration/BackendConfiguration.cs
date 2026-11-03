namespace Behind.Core.Common.Configuration;

public sealed record BackendConfiguration
{
    public const string SectionName = "Backend";
    public const string VfsDirectoryName = "vfs";

    public string? DataDirectory { get; init; }

    public string VfsRoot =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? throw new InvalidOperationException(
                $"'{SectionName}:{nameof(DataDirectory)}' must be set in backend.json.")
            : Path.Combine(DataDirectory, VfsDirectoryName);
}
