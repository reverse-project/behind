using System.Text.Json;
using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;

namespace Behind.Tests.CoreCommon;

public class DatabaseProviderConverterTests
{
    private readonly ProviderTypeConverter _converter = new();

    [Theory]
    [InlineData("Postgres")]
    [InlineData("postgres")]
    [InlineData("PostgreSQL")]
    [InlineData("Npgsql")]
    public void ConvertFrom_PostgresNames_ReturnsPostgres(string value)
    {
        Assert.IsType<Postgres>(_converter.ConvertFrom(value));
    }

    [Theory]
    [InlineData("Sqlite")]
    [InlineData("sqlite")]
    public void ConvertFrom_SqliteNames_ReturnsSqlite(string value)
    {
        Assert.IsType<Sqlite>(_converter.ConvertFrom(value));
    }

    [Fact]
    public void ConvertFrom_UnknownProvider_ThrowsFormatException()
    {
        var exception = Assert.Throws<FormatException>(() => _converter.ConvertFrom("Oracle"));

        Assert.Contains(nameof(Postgres), exception.Message);
        Assert.Contains(nameof(Sqlite), exception.Message);
    }

    [Theory]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public void Json_RoundTripsProviderValue(string value)
    {
        var provider = JsonSerializer.Deserialize<Provider>($"\"{value}\"");

        Assert.NotNull(provider);
        Assert.Equal(value, provider.Value);
        Assert.Equal($"\"{value}\"", JsonSerializer.Serialize(provider));
    }

    [Fact]
    public void Json_Write_UnsupportedProvider_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize<Provider>(new UnsupportedProvider()));
    }

    private sealed record UnsupportedProvider() : Provider("Unsupported");
}
