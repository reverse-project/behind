using Behind.Common.Scheduling;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Core.Database.Converters;

public sealed class CronExpressionConverter() : ValueConverter<CronExpression, string>(
    expression => expression.Text,
    text => FromText(text))
{
    public static CronExpression FromText(string text) =>
        CronExpression.Parse(text).Match(
            errors => throw new FormatException(string.Join("; ", errors.Select(error => error.Message))),
            expression => expression);
}
