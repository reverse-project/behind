using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public static class SiteImageExtensions
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    private sealed record Signature(string ContentType, int Offset, byte[] Bytes);

    private static readonly byte[] RiffMarker = "RIFF"u8.ToArray();
    private static readonly byte[] WebpMarker = "WEBP"u8.ToArray();

    private static readonly IReadOnlyList<Signature> Signatures =
    [
        new("image/png", 0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        new("image/jpeg", 0, [0xFF, 0xD8, 0xFF]),
        new("image/gif", 0, "GIF87a"u8.ToArray()),
        new("image/gif", 0, "GIF89a"u8.ToArray())
    ];

    extension(byte[] content)
    {
        public Option<string> DetectImageContentType() =>
            content.IsWebp
                ? Some("image/webp")
                : Signatures.FirstOrDefault(signature => content.Matches(signature)) switch
                {
                    { } found => Some(found.ContentType),
                    _ => None
                };

        private bool IsWebp =>
            content.Length >= 12
            && content.AsSpan(0, 4).SequenceEqual(RiffMarker)
            && content.AsSpan(8, 4).SequenceEqual(WebpMarker);

        private bool Matches(Signature signature) =>
            content.Length >= signature.Offset + signature.Bytes.Length
            && content.AsSpan(signature.Offset, signature.Bytes.Length).SequenceEqual(signature.Bytes);
    }

    extension(string? fileName)
    {
        public string ToSiteImageFileName(string fallback)
        {
            var name = (fileName ?? string.Empty).Split(PathSeparators).Last().Trim();
            var limited = name.Length > SiteImage.MaxFileNameLength ? name[..SiteImage.MaxFileNameLength] : name;

            return limited.Length == 0 ? fallback : limited;
        }
    }
}
