namespace Behind.Core.Domain;

public sealed record UserAgent(string Value)
{
    public const int MaxLength = 1000;

    public static implicit operator UserAgent(string value) => new(value);
    public static implicit operator string(UserAgent userAgent) => userAgent.Value;

    public override string ToString() => Value;
}