using System.ComponentModel;
using System.Text.Json;
using Behind.Sport.Domain;

namespace Behind.Tests.Sport;

public sealed class SportDomainConversionTests
{
    [Theory]
    [InlineData(Side.Left, "\"Left\"")]
    [InlineData(Side.Right, "\"Right\"")]
    [InlineData(Side.Both, "\"Both\"")]
    public void Side_RoundTripsThroughJsonAndTypeDescriptor(Side value, string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(value));
        Assert.Equal(value, JsonSerializer.Deserialize<Side>(json));
        var converter = TypeDescriptor.GetConverter(typeof(Side));
        Assert.Equal(value.ToString(), converter.ConvertToInvariantString(value));
        Assert.Equal(value, converter.ConvertFromInvariantString(value.ToString()));
    }

    [Theory]
    [InlineData(StatScope.Player, "\"Player\"")]
    [InlineData(StatScope.Team, "\"Team\"")]
    public void StatScope_RoundTripsThroughJsonAndTypeDescriptor(StatScope value, string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(value));
        Assert.Equal(value, JsonSerializer.Deserialize<StatScope>(json));
        var converter = TypeDescriptor.GetConverter(typeof(StatScope));
        Assert.Equal(value.ToString(), converter.ConvertToInvariantString(value));
        Assert.Equal(value, converter.ConvertFromInvariantString(value.ToString()));
    }
}
