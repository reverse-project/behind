namespace Behind.Core.Domain;

public sealed record RemoteAddress(string Value)
{
    public const int MaxLength = 255;

    public static implicit operator RemoteAddress(string value) => new(value);
    public static implicit operator string(RemoteAddress remoteAddress) => remoteAddress.Value;

    public override string ToString() => Value;
}