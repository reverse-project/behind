using System.Text.Json;

namespace Behind.Core.Tools;

public static class ReleaseNotesGenerator
{
    public static string Generate(string changelogJson, string? version = null)
    {
        using JsonDocument document = JsonDocument.Parse(changelogJson);
        JsonElement releases = document.RootElement;
        if (releases.ValueKind != JsonValueKind.Array || releases.GetArrayLength() == 0)
        {
            throw new InvalidDataException("The changelog must be a non-empty JSON array.");
        }

        JsonElement release;
        if (version is null)
        {
            release = releases[0];
        }
        else
        {
            release = releases.EnumerateArray()
                .FirstOrDefault(candidate =>
                    candidate.ValueKind == JsonValueKind.Object &&
                    candidate.TryGetProperty("version", out JsonElement candidateVersion) &&
                    candidateVersion.ValueKind == JsonValueKind.String &&
                    candidateVersion.GetString() == version);
            if (release.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidDataException($"Version '{version}' was not found in the changelog.");
            }
        }

        string releaseVersion = GetRequiredString(release, "version", "The selected release");
        string date = GetRequiredString(release, "date", "The selected release");
        if (!release.TryGetProperty("changes", out JsonElement changes) ||
            changes.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The selected release must have a changes array.");
        }

        var markdown = new List<string>
        {
            $"## v{EscapeCell(releaseVersion)} ({EscapeCell(date)})",
            "",
            "| Type | Change |",
            "| --- | --- |"
        };
        int index = 0;
        foreach (JsonElement change in changes.EnumerateArray())
        {
            index++;
            string type = GetRequiredString(change, "type", $"Change {index}");
            string text = GetRequiredString(change, "text", $"Change {index}");
            string label = char.ToUpperInvariant(type[0]) + type[1..];
            markdown.Add($"| {EscapeCell(label)} | {EscapeCell(text)} |");
        }

        return string.Join(Environment.NewLine, markdown) + Environment.NewLine;
    }

    private static string GetRequiredString(JsonElement element, string property, string description)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{description} must have a non-empty '{property}' string.");
        }

        string? text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException($"{description} must have a non-empty '{property}' string.");
        }

        return text;
    }

    private static string EscapeCell(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
