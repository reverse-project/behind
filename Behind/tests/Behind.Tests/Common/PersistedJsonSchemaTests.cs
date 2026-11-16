using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Common.Modules;
using Behind.Contact.Domain;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.Database.Converters;
using Behind.Core.WebApi.Layouts;
using Microsoft.Extensions.Options;
using static Behind.Core.Domain.ScheduleExtensions;

namespace Behind.Tests.Common;

public sealed class PersistedJsonSchemaTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("layouts-1.0.0", """{"layouts":"invalid"}""")]
    [InlineData("layouts-1.0.0", """{"layouts":[{"sections":[{"widgets":[{"row":true}]}]}]}""")]
    [InlineData("contact-subjects-1.0.0", """[123]""")]
    [InlineData("contact-fields-1.0.0", """[{"key":"agree","type":"unknown"}]""")]
    [InlineData("contact-fields-1.0.0", """[{"required":"true"}]""")]
    [InlineData("contact-values-1.0.0", """[{"key":"name","value":123}]""")]
    [InlineData("schedule-parameters-1.0.0", """{"days":30}""")]
    [InlineData("schedule-participants-1.0.0", """[{"kind":true}]""")]
    public void KnownSchemas_RejectMalformedJsonStructures(string schema, string json)
    {
        Assert.False(JsonSchemas.IsValid(schema, JsonNode.Parse(json)));
    }

    [Fact]
    public void ContactConverters_ValidateActualWireShapesAndRoundTrip()
    {
        var fields = ContactFieldSchema.From([new ContactField("agree", "Consent", ContactFieldType.Boolean, true, 0)]);
        var values = ContactFieldValues.From([new ContactValue("agree", "Consent", "true")]);
        var subjects = ContactSubjects.From(["Support"]);

        RoundTrip("contact-fields-1.0.0", fields);
        RoundTrip("contact-values-1.0.0", values);
        RoundTrip("contact-subjects-1.0.0", subjects);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContactFieldValues>("""[{"value":true}]""", Web));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContactFieldSchema>("""[{"type":"unknown"}]""", Web));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContactSubjects>("[1]", Web));
    }

    [Fact]
    public void ScheduleSerialization_KeepsLegacyNullDefaultsAndCaseInsensitiveProperties()
    {
        Assert.Empty(ParseScheduleParticipants("null").Items);
        Assert.Empty(ParseScheduleParameters("null").Values);
        Assert.Empty(ParseScheduleParticipants(string.Empty).Items);
        var participants = ParseScheduleParticipants("""[{"KIND":" Email ","VALUE":"someone@example.org"}]""");
        Assert.True(JsonSchemas.IsValid("schedule-participants-1.0.0", JsonNode.Parse(participants.ToJson())));
        Assert.Equal("someone@example.org", Assert.Single(participants.Items).Value);
        var parameters = ParseScheduleParameters("""{"days":"30","optional":null}""");
        Assert.True(JsonSchemas.IsValid("schedule-parameters-1.0.0", JsonNode.Parse(parameters.ToJson())));
        Assert.Equal("30", parameters.Values["days"]);
        Assert.Throws<JsonException>(() => ParseScheduleParticipants("""[{"kind":true}]"""));
        Assert.Throws<JsonException>(() => ParseScheduleParameters("""{"days":30}"""));
    }

    [Fact]
    public async Task SavedLayout_PassesSchemaAndRetainsRevisionAndBusinessValidation()
    {
        var directory = Directory.CreateTempSubdirectory("behind-schema-layout-");
        try
        {
            using var service = new LayoutService(
                Options.Create(new BackendConfiguration { DataDirectory = directory.FullName }), ModuleCatalog.Empty);
            var configuration = await service.Read(CancellationToken.None);
            Assert.True(JsonSchemas.IsValid("layouts-1.0.0", JsonSerializer.SerializeToNode(configuration, Web)));
            var saved = await service.Save(configuration, false, CancellationToken.None);
            Assert.Null(saved.Error);
            var path = Path.Combine(directory.FullName, "layouts.json");
            var json = await File.ReadAllTextAsync(path);
            Assert.True(JsonSchemas.IsValid("layouts-1.0.0", JsonNode.Parse(json)));
            Assert.Equal(1, saved.Configuration!.Revision);
            var invalid = await service.Save(saved.Configuration with { Layouts = [] }, false, CancellationToken.None);
            Assert.Equal("invalid", invalid.Error);
            Assert.Equal(json, await File.ReadAllTextAsync(path));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void AuditStorage_PreservesDiscriminatorsAndArbitraryDifferenceValues()
    {
        AuditLogEntry.AuditAction[] actions =
        [
            new AuditLogEntry.Created(),
            new AuditLogEntry.Updated([new Behind.Common.PropertyDifference("Field", null, new { Text = "value" })]),
            new AuditLogEntry.ChangeOwnership(Guid.NewGuid(), Guid.NewGuid()),
            new AuditLogEntry.Moved(Guid.NewGuid(), Guid.NewGuid())
        ];
        foreach (var action in actions)
        {
            var stored = AuditLogEntryAuditActionJsonConverter.ToStorageValue(action);
            var node = stored.StartsWith('{') ? JsonNode.Parse(stored) : JsonValue.Create(stored);
            Assert.True(JsonSchemas.IsValid("audit-action-1.0.0", node));
            Assert.Equal(action.GetType(), AuditLogEntryAuditActionJsonConverter.FromStorageValue(stored).GetType());
        }
    }

    private static void RoundTrip<T>(string schema, T value)
    {
        foreach (var options in new[] { Web, new JsonSerializerOptions() })
        {
            var json = JsonSerializer.Serialize(value, options);
            Assert.True(JsonSchemas.IsValid(schema, JsonNode.Parse(json)));
            Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(json, options), options));
        }
    }
}
