namespace Behind.Core.Domain;

public sealed record SiteImage(string FileName, string ContentType, byte[] Content, DateTimeOffset CreatedAt)
{
    public const int MaxFileNameLength = 200;
}
