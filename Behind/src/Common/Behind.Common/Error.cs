namespace Behind.Common;

public static partial class FunctionalExtensions
{
    public static Error Error(string message) => new Error(message);
}

public class Error
{
    public virtual string? Message { get; }
    public override string ToString() => Message ?? string.Empty;
    protected Error() { }
    internal Error(string message) { Message = message; }

    public static implicit operator Error(string m) => new(m);
}