using Behind.Common;

namespace Behind.Core.Domain;

public static class DownloadExtensions
{
    extension(string? displayName)
    {
        public string ToDownloadDisplayName(string fallback) =>
            (string.IsNullOrWhiteSpace(displayName) ? fallback.Trim() : displayName.Trim())
                .Shortened(Download.MaxDisplayNameLength);
    }
}
