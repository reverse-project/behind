namespace Behind.Core.Domain;

public static class CarouselExtensions
{
    private static readonly string[] AllowedSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];

    extension(string? link)
    {
        public bool IsSafeCarouselLink =>
            string.IsNullOrEmpty(link)
            || (link.Length <= CarouselSlide.MaxLinkUrlLength
                && link.Trim() == link
                && (link.IsLocalCarouselLink || link.IsAllowedAbsoluteCarouselLink));

        private bool IsLocalCarouselLink =>
            link is not null
            && (link.StartsWith("#/", StringComparison.Ordinal)
                || (link.StartsWith('/') && !link.StartsWith("//", StringComparison.Ordinal) && !link.Contains('\\')));

        private bool IsAllowedAbsoluteCarouselLink =>
            Uri.TryCreate(link, UriKind.Absolute, out var uri) && AllowedSchemes.Contains(uri.Scheme);
    }
}
