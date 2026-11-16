namespace Behind.Common;

public static class SensitiveFieldMaskingExtensions
{
    public static string MaskSensitiveValue(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length < 6)
        {
            return $"{value[0]}..{value[^1]}";
        }

        var middleLength = value.Length - 4;
        var middleDots = new string('.', middleLength);
        return $"{value[..2]}{middleDots}{value[^2..]}";
    }
}
