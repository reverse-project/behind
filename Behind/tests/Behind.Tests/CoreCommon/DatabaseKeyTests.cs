using System.ComponentModel;
using System.Text.Json;
using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;

namespace Behind.Tests.CoreCommon;

public sealed class DatabaseKeyTests
{
    [Fact]
    public void TypeConverter_IsDiscoverableAndCreatesDatabaseKey()
    {
        var converter = TypeDescriptor.GetConverter(typeof(DatabaseKey));

        Assert.IsType<DatabaseKeyTypeConverter>(converter);
        Assert.Equal(new DatabaseKey("Main"), converter.ConvertFromInvariantString("Main"));
    }

    [Fact]
    public void Json_RoundTripsAsTheExistingStringFormat()
    {
        var key = new DatabaseKey("Main");

        Assert.Equal("\"Main\"", JsonSerializer.Serialize(key));
        Assert.Equal(key, JsonSerializer.Deserialize<DatabaseKey>("\"Main\""));
        Assert.Equal("Main", key.ToString());
        string value = key;
        Assert.Equal("Main", value);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("\"\"")]
    [InlineData("\" \"")]
    public void Json_RejectsInvalidKeys(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DatabaseKey>(json));
    }

    [Fact]
    public void Configuration_KeepsKeyPropertyNameAndUsesDatabaseKey()
    {
        Assert.Equal(typeof(Option<DatabaseKey>), typeof(DatabaseConfiguration).GetProperty("Key")!.PropertyType);
    }
}
