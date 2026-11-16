namespace Behind.Core.Common.Configuration;

public sealed record ContentRestrictionsConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:ContentRestrictions";

    public bool Enabled { get; init; }
}
