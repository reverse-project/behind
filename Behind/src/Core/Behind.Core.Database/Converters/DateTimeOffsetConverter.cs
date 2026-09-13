using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Core.Database.Converters;

public sealed class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetConverter() : base(
        d => d.UtcTicks,
        l => new DateTimeOffset(l, TimeSpan.Zero)
    )
    {
        
    }
}