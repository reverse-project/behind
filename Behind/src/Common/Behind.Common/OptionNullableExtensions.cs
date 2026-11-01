namespace Behind.Common;

public static class OptionNullableExtensions
{
    extension<T>(Option<T> option) where T : struct
    {
        public T? ToNullable() => option.Match(() => (T?)null, value => value);
    }
}
