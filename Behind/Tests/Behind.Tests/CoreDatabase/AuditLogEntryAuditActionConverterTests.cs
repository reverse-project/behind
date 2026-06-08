using System.Text.Json;
using Behind.Core.Database.Converters;

namespace Behind.Tests.CoreDatabase;

public sealed class AuditLogEntryAuditActionConverterTests
{
    [Fact]
    public void ToStorageValue_Created_ReturnsPlainActionName()
    {
        var action = new AuditLogEntry.Created();

        string value = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);

        Assert.Equal(nameof(AuditLogEntry.Created), value);
    }

    [Fact]
    public void ToStorageValue_Deleted_ReturnsPlainActionName()
    {
        var action = new AuditLogEntry.Deleted();

        string value = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);

        Assert.Equal(nameof(AuditLogEntry.Deleted), value);
    }

    [Fact]
    public void ToStorageValue_Updated_ReturnsJson()
    {
        var action = new AuditLogEntry.Updated("""{"Name":"Old"}""", """{"Name":"New"}""");

        string value = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);

        Assert.StartsWith("{", value.TrimStart());
    }

    [Fact]
    public void ToStorageValue_Updated_WritesValueDiscriminator()
    {
        var action = new AuditLogEntry.Updated("""{"Name":"Old"}""", """{"Name":"New"}""");

        string value = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);

        using JsonDocument document = JsonDocument.Parse(value);
        Assert.Equal(nameof(AuditLogEntry.Updated), document.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void ToStorageValue_Updated_WritesOldValues()
    {
        var action = new AuditLogEntry.Updated("""{"Name":"Old"}""", """{"Name":"New"}""");

        string value = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);

        using JsonDocument document = JsonDocument.Parse(value);
        Assert.Equal("""{"Name":"Old"}""", document.RootElement.GetProperty("oldValues").GetString());
    }

    [Fact]
    public void ToStorageValue_Updated_WritesNewValues()
    {
        var action = new AuditLogEntry.Updated("""{"Name":"Old"}""", """{"Name":"New"}""");

        string value = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);

        using JsonDocument document = JsonDocument.Parse(value);
        Assert.Equal("""{"Name":"New"}""", document.RootElement.GetProperty("newValues").GetString());
    }

    [Fact]
    public void FromStorageValue_CreatedPlainText_ReturnsCreated()
    {
        AuditLogEntry.AuditAction action =
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(nameof(AuditLogEntry.Created));

        Assert.IsType<AuditLogEntry.Created>(action);
    }

    [Fact]
    public void FromStorageValue_DeletedPlainText_ReturnsDeleted()
    {
        AuditLogEntry.AuditAction action =
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(nameof(AuditLogEntry.Deleted));

        Assert.IsType<AuditLogEntry.Deleted>(action);
    }

    [Fact]
    public void FromStorageValue_UpdatedJson_ReturnsUpdated()
    {
        string value = """
        {
          "value": "Updated",
          "oldValues": "{\"Name\":\"Old\"}",
          "newValues": "{\"Name\":\"New\"}"
        }
        """;

        AuditLogEntry.AuditAction action =
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(value);

        Assert.IsType<AuditLogEntry.Updated>(action);
    }

    [Fact]
    public void FromStorageValue_UpdatedJson_PreservesOldValues()
    {
        string value = """
        {
          "value": "Updated",
          "oldValues": "{\"Name\":\"Old\"}",
          "newValues": "{\"Name\":\"New\"}"
        }
        """;

        var action = Assert.IsType<AuditLogEntry.Updated>(
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(value));

        Assert.Equal("""{"Name":"Old"}""", action.OldValues);
    }

    [Fact]
    public void FromStorageValue_UpdatedJson_PreservesNewValues()
    {
        string value = """
        {
          "value": "Updated",
          "oldValues": "{\"Name\":\"Old\"}",
          "newValues": "{\"Name\":\"New\"}"
        }
        """;

        var action = Assert.IsType<AuditLogEntry.Updated>(
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(value));

        Assert.Equal("""{"Name":"New"}""", action.NewValues);
    }

    [Fact]
    public void FromStorageValue_UpdatedPlainText_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(nameof(AuditLogEntry.Updated)));
    }

    [Fact]
    public void FromStorageValue_UnknownPlainText_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            AuditLogEntryAuditActionJsonConverter.FromStorageValue("Moved"));
    }

    [Fact]
    public void FromStorageValue_EmptyText_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(""));
    }

    [Fact]
    public void FromStorageValue_WhitespaceText_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            AuditLogEntryAuditActionJsonConverter.FromStorageValue("   "));
    }

    [Fact]
    public void ToStorageValue_Updated_RoundTrips()
    {
        var original = new AuditLogEntry.Updated("""{"Name":"Old"}""", """{"Name":"New"}""");

        string storageValue = AuditLogEntryAuditActionJsonConverter.ToStorageValue(original);
        var result = Assert.IsType<AuditLogEntry.Updated>(
            AuditLogEntryAuditActionJsonConverter.FromStorageValue(storageValue));

        Assert.Equal(original.OldValues, result.OldValues);
        Assert.Equal(original.NewValues, result.NewValues);
    }

    [Fact]
    public void TypeConverter_CanConvertFromString()
    {
        var converter = new AuditLogEntryAuditActionTypeConverter();

        Assert.True(converter.CanConvertFrom(typeof(string)));
    }

    [Fact]
    public void TypeConverter_CanConvertToString()
    {
        var converter = new AuditLogEntryAuditActionTypeConverter();

        Assert.True(converter.CanConvertTo(typeof(string)));
    }

    [Fact]
    public void TypeConverter_ConvertFrom_Created_ReturnsCreated()
    {
        var converter = new AuditLogEntryAuditActionTypeConverter();

        var action = converter.ConvertFromInvariantString(nameof(AuditLogEntry.Created));

        Assert.IsType<AuditLogEntry.Created>(action);
    }

    [Fact]
    public void TypeConverter_ConvertFrom_UpdatedJson_ReturnsUpdated()
    {
        var converter = new AuditLogEntryAuditActionTypeConverter();

        object? action = converter.ConvertFromInvariantString("""
        {
          "value": "Updated",
          "oldValues": "{\"Name\":\"Old\"}",
          "newValues": "{\"Name\":\"New\"}"
        }
        """);

        Assert.IsType<AuditLogEntry.Updated>(action);
    }

    [Fact]
    public void TypeConverter_ConvertTo_Created_ReturnsActionName()
    {
        var converter = new AuditLogEntryAuditActionTypeConverter();

        string? value = converter.ConvertToInvariantString(new AuditLogEntry.Created());

        Assert.Equal(nameof(AuditLogEntry.Created), value);
    }

    [Fact]
    public void TypeConverter_ConvertTo_Updated_ReturnsJson()
    {
        var converter = new AuditLogEntryAuditActionTypeConverter();
        var action = new AuditLogEntry.Updated("""{"Name":"Old"}""", """{"Name":"New"}""");

        string? value = converter.ConvertToInvariantString(action);

        Assert.NotNull(value);
        Assert.StartsWith("{", value.TrimStart());
    }
}