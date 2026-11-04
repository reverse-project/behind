using System.Text.Json;
using Behind.Common.Database.Raw;

namespace Behind.Tests.CommonDatabase;

public class RawColumnExtensionsTests
{
    private static RawColumn Column(RawValueKind kind, bool sensitive = false) =>
        new("Value", "TEXT", kind, true, false, sensitive);

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);

    private static object? ParsedValue(RawColumn column, JsonElement element) =>
        column.Parse(element).Match<object?>(_ => null, value => value.Value);

    [Fact]
    public void Parse_Text_KeepsStringsAndRawJson()
    {
        var column = Column(RawValueKind.Text);

        Assert.Equal("abc", ParsedValue(column, Json("abc")));
        Assert.Equal("12", ParsedValue(column, Json(12)));
    }

    [Fact]
    public void Parse_Integer_AcceptsNumbersAndNumericText()
    {
        var column = Column(RawValueKind.Integer);

        Assert.Equal(7L, ParsedValue(column, Json(7)));
        Assert.Equal(8L, ParsedValue(column, Json("8")));
        Assert.True(column.Parse(Json("eight")).Match(errors => errors.Any(), _ => false));
        Assert.True(column.Parse(Json(true)).Match(errors => errors.Any(), _ => false));
    }

    [Fact]
    public void Parse_Number_AcceptsDecimals()
    {
        Assert.Equal(1.5, ParsedValue(Column(RawValueKind.Number), Json(1.5)));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("\"true\"", true)]
    public void Parse_Boolean_AcceptsFlagsAndNumbers(string json, bool expected)
    {
        var element = JsonDocument.Parse(json).RootElement;

        Assert.Equal(expected, ParsedValue(Column(RawValueKind.Boolean), element));
    }

    [Fact]
    public void Parse_Binary_DecodesBase64AndAllowsEmpty()
    {
        var column = Column(RawValueKind.Binary);

        Assert.Equal(new byte[] { 1, 2, 3 }, ParsedValue(column, Json(Convert.ToBase64String([1, 2, 3]))));
        Assert.Equal(Array.Empty<byte>(), ParsedValue(column, Json("")));
        Assert.True(column.Parse(Json("***")).Match(errors => errors.Any(), _ => false));
    }

    [Fact]
    public void Parse_Guid_RequiresAValidGuid()
    {
        var id = Guid.NewGuid();
        var column = Column(RawValueKind.Guid);

        Assert.Equal(id, ParsedValue(column, Json(id.ToString())));
        Assert.True(column.Parse(Json("nope")).Match(errors => errors.Any(), _ => false));
    }

    [Fact]
    public void Parse_DateTime_ReadsInstants()
    {
        var parsed = ParsedValue(Column(RawValueKind.DateTime), Json("2026-11-04T17:00:00+00:00"));

        Assert.Equal(new DateTimeOffset(2026, 11, 4, 17, 0, 0, TimeSpan.Zero), parsed);
    }

    [Fact]
    public void Parse_Null_BecomesDbNull()
    {
        Assert.Equal(DBNull.Value, ParsedValue(Column(RawValueKind.Integer), Json(null)));
    }

    [Fact]
    public void Normalize_ConvertsStoredValuesToJsonFriendlyOnes()
    {
        var id = Guid.NewGuid();

        Assert.Null(Column(RawValueKind.Text).Normalize(DBNull.Value));
        Assert.Equal("AQID", Column(RawValueKind.Binary).Normalize(new byte[] { 1, 2, 3 }));
        Assert.Equal(id.ToString(), Column(RawValueKind.Guid).Normalize(id.ToString().ToUpperInvariant()));
        Assert.Equal(true, Column(RawValueKind.Boolean).Normalize(1L));
        Assert.Equal(false, Column(RawValueKind.Boolean).Normalize(0L));
        Assert.Equal(5L, Column(RawValueKind.Integer).Normalize(5L));
    }

    [Fact]
    public void Present_MasksSensitiveValuesButNotNulls()
    {
        var column = Column(RawValueKind.Text, sensitive: true);

        Assert.Equal(SensibleExtensions.MaskedValue, column.Present("secret"));
        Assert.Null(column.Present(null));
        Assert.Equal("open", Column(RawValueKind.Text).Present("open"));
    }

    [Fact]
    public void IsMasked_OnlyMatchesSensitiveColumnsHoldingTheMask()
    {
        var mask = Json(SensibleExtensions.MaskedValue);

        Assert.True(Column(RawValueKind.Text, sensitive: true).IsMasked(mask));
        Assert.False(Column(RawValueKind.Text).IsMasked(mask));
        Assert.False(Column(RawValueKind.Text, sensitive: true).IsMasked(Json("other")));
    }

    [Fact]
    public void IsSearchable_ExcludesBinaryAndSensitiveColumns()
    {
        Assert.True(Column(RawValueKind.Text).IsSearchable);
        Assert.False(Column(RawValueKind.Binary).IsSearchable);
        Assert.False(Column(RawValueKind.Text, sensitive: true).IsSearchable);
    }
}
