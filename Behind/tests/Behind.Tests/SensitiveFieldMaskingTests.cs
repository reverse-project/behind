namespace Behind.Tests.Sensitive;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Converters;

public sealed class SensitiveFieldMaskingTests
{
    [Fact]
    public void MaskSensitiveValue_WithLengthLessThanSix_MasksFirstAndLastCharacter()
    {
        var value = "pass";
        var result = value.MaskSensitiveValue();
        Assert.Equal("p..s", result);
    }

    [Fact]
    public void MaskSensitiveValue_WithLengthOfFive_MasksFirstAndLastCharacter()
    {
        var value = "admin";
        var result = value.MaskSensitiveValue();
        Assert.Equal("a..n", result);
    }

    [Fact]
    public void MaskSensitiveValue_WithLengthSixOrMore_MasksFirstTwoAndLastTwoCharacters()
    {
        var value = "password123456";
        var result = value.MaskSensitiveValue();
        Assert.Equal("pa..........56", result);
    }

    [Fact]
    public void MaskSensitiveValue_WithLengthOfSix_MasksFirstTwoAndLastTwoCharacters()
    {
        var value = "secret";
        var result = value.MaskSensitiveValue();
        Assert.Equal("se..et", result);
        Assert.Equal(6, result.Length);
    }

    [Fact]
    public void MaskSensitiveValue_WithSingleCharacter_ReturnsOriginalValue()
    {
        var value = "a";
        var result = value.MaskSensitiveValue();
        Assert.Equal("a..a", result);
    }

    [Fact]
    public void MaskSensitiveValue_WithTwoCharacters_ReturnsMasked()
    {
        var value = "ab";
        var result = value.MaskSensitiveValue();
        Assert.Equal("a..b", result);
    }

    [Fact]
    public void MaskSensitiveValue_WithEmptyString_ReturnsEmptyString()
    {
        var value = "";
        var result = value.MaskSensitiveValue();
        Assert.Empty(result);
    }

    [Fact]
    public void MaskSensitiveValue_WithNullString_ReturnsNull()
    {
        string? value = null;
        var result = value?.MaskSensitiveValue();
        Assert.Null(result);
    }

    [Fact]
    public void SensitiveContext_WhenCreated_IsActive()
    {
        using var ctx = new SensitiveContext();
        Assert.True(ctx.IsActive);
    }

    [Fact]
    public void SensitiveContext_WhenDisposed_IsNotActive()
    {
        var ctx = new SensitiveContext();
        ctx.Dispose();
        Assert.False(ctx.IsActive);
    }

    [Fact]
    public void SensitiveContext_WhenDisposedTwice_DoesNotThrow()
    {
        var ctx = new SensitiveContext();
        ctx.Dispose();
        var exception = Record.Exception(() => ctx.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void SensitiveContext_GetCurrent_ReturnsActiveContext()
    {
        using var ctx = new SensitiveContext();
        var current = SensitiveContext.GetCurrent();
        Assert.Same(ctx, current);
    }

    [Fact]
    public void SensitiveContext_GetCurrent_ReturnsNullWhenNoContextActive()
    {
        var current = SensitiveContext.GetCurrent();
        Assert.Null(current);
    }

    [Fact]
    public void SensitiveContext_VerifyContextActive_ThrowsWhenNoContextActive()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SensitiveContext.VerifyContextActive());
        Assert.Contains("SensitiveContext is not active", exception.Message);
    }

    [Fact]
    public void SensitiveContext_VerifyContextActive_DoesNotThrowWhenContextActive()
    {
        using var ctx = new SensitiveContext();
        var exception = Record.Exception(() => SensitiveContext.VerifyContextActive());
        Assert.Null(exception);
    }

    [Fact]
    public void JsonSerialization_WithSensibleAttribute_MasksFieldWhenContextActive()
    {
        var user = new TestUser("john_doe@example.com");
        var options = new JsonSerializerOptions { WriteIndented = false }
            .WithSensitiveContext(null);

        using var ctx = new SensitiveContext();
        options = new JsonSerializerOptions { WriteIndented = false }
            .WithSensitiveContext(ctx);

        var json = JsonSerializer.Serialize(user, options);
        Assert.Contains("jo................om", json);
        Assert.DoesNotContain("john_doe@example.com", json);
    }

    [Fact]
    public void JsonSerialization_WithSensibleAttribute_ThrowsWhenContextNotActive()
    {
        var user = new TestUser("secret_password_123");
        var options = new JsonSerializerOptions { WriteIndented = false };
        options.Converters.Add(new SensibleJsonConverterFactory());

        var exception = Assert.Throws<InvalidOperationException>(
            () => JsonSerializer.Serialize(user, options));
        Assert.Contains("SensitiveContext is not active", exception.Message);
    }

    [Fact]
    public void JsonSerialization_WithMultipleSensibleFields_MasksAllOfThem()
    {
        var testData = new TestDataWithMultipleSensibles("password123", "secretkey", "pincode12345");
        using var ctx = new SensitiveContext();
        var options = new JsonSerializerOptions { WriteIndented = false }
            .WithSensitiveContext(ctx);

        var json = JsonSerializer.Serialize(testData, options);
        Assert.Contains("pa.......23", json);
        Assert.Contains("se.....ey", json);
        Assert.Contains("pi........45", json);
    }

    [Fact]
    public void JsonSerialization_WithMixedSensibleAndNormalFields_MasksOnlyMarkedFields()
    {
        var testData = new TestMixedFields("sensitive_value", "normal_value");
        using var ctx = new SensitiveContext();
        var options = new JsonSerializerOptions { WriteIndented = false }
            .WithSensitiveContext(ctx);

        var json = JsonSerializer.Serialize(testData, options);
        Assert.Contains("se...........ue", json);
        Assert.Contains("normal_value", json);
    }

    [Fact]
    public void JsonSerialization_WithValueType_MasksSensitiveFields()
    {
        var valueType = new TestValueType("secret123");
        using var ctx = new SensitiveContext();
        var options = new JsonSerializerOptions { WriteIndented = false }
            .WithSensitiveContext(ctx);

        var json = JsonSerializer.Serialize(valueType, options);
        Assert.Contains("se.....23", json);
    }

    [Fact]
    public void ConfigurationTransformation_MasksConnectionStringsValues()
    {
        var value1 = "Server=localhost;Database=test";
        var value2 = "Data Source=/path/to/database.db";
        
        Assert.Equal(30, value1.Length);
        Assert.Equal(32, value2.Length);
        
        var masked1 = value1.MaskSensitiveValue();
        var masked2 = value2.MaskSensitiveValue();
        
        Assert.Equal(30, masked1.Length);
        Assert.Equal(32, masked2.Length);
        
        using var jsonDoc = System.Text.Json.JsonDocument.Parse("""
            {
                "ConnectionStrings": {
                    "DefaultConnection": "Server=localhost;Database=test",
                    "SecondaryConnection": "Data Source=/path/to/database.db"
                },
                "Database": {
                    "Provider": "Sqlite"
                }
            }
            """);

        var node = System.Text.Json.Nodes.JsonNode.Parse(jsonDoc.RootElement.GetRawText());
        var masked = node.MaskSensitiveConfigurationValues("ConnectionStrings");

        var connectionStrings = masked?["ConnectionStrings"];
        Assert.NotNull(connectionStrings);
        Assert.Contains(masked1, connectionStrings.ToString());
        Assert.Contains(masked2, connectionStrings.ToString());
        Assert.DoesNotContain("localhost", connectionStrings.ToString());
        Assert.DoesNotContain("path/to/database", connectionStrings.ToString());
    }

    [Fact]
    public void ConfigurationTransformation_PreservesOtherSections()
    {
        using var jsonDoc = System.Text.Json.JsonDocument.Parse("""
            {
                "ConnectionStrings": {
                    "DefaultConnection": "Server=localhost;Database=test"
                },
                "Database": {
                    "Provider": "Sqlite"
                }
            }
            """);

        var node = System.Text.Json.Nodes.JsonNode.Parse(jsonDoc.RootElement.GetRawText());
        var masked = node.MaskSensitiveConfigurationValues("ConnectionStrings");

        var database = masked?["Database"];
        Assert.NotNull(database);
        Assert.Contains("Sqlite", database.ToString());
    }

    private record TestUser([property: Sensible] string Email);

    private record TestDataWithMultipleSensibles(
        [property: Sensible] string Password,
        [property: Sensible] string ApiKey,
        [property: Sensible] string PinCode);

    private record TestMixedFields(
        [property: Sensible] string SensitiveField,
        string NormalField);

    [Fact]
    public void ConfigurationFieldMasking_MasksNestedObjectFields()
    {
        var json = JsonNode.Parse("""
            {
                "Security": {
                    "MagicLink": {
                        "ActualSecret": "secret-guid-value-12345",
                        "OldSecret": "old-secret-guid-value"
                    },
                    "ConnectionStrings": {
                        "Default": "Server=localhost;Database=test"
                    }
                }
            }
            """);

        var masked = json
            .MaskNestedFields(new[] { "Security", "MagicLink" }, "ActualSecret", "OldSecret")
            .MaskSensitiveConfigurationValues("ConnectionStrings");

        var magicLink = masked?["Security"]?["MagicLink"] as JsonObject;
        Assert.NotNull(magicLink);

        var actualSecret = magicLink["ActualSecret"]?.GetValue<string>();
        var oldSecret = magicLink["OldSecret"]?.GetValue<string>();

        Assert.Equal(23, actualSecret?.Length);
        Assert.Equal(21, oldSecret?.Length);

        Assert.StartsWith("se", actualSecret);
        Assert.EndsWith("45", actualSecret);
        Assert.StartsWith("ol", oldSecret);
        Assert.EndsWith("ue", oldSecret);
    }

    private struct TestValueType
    {
        [Sensible]
        public string Secret { get; }

        public TestValueType(string secret)
        {
            Secret = secret;
        }
    }
}
