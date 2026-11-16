using Microsoft.Extensions.Options;

namespace Behind.Tests.Helpers;

public sealed class FixedOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}

public static class FixedOptionsSnapshot
{
    public static FixedOptionsSnapshot<T> Of<T>(T value)
        where T : class => new(value);
}
