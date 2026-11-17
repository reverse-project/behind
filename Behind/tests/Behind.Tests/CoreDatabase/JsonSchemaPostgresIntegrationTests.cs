using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.Modules;
using Behind.Common.Scheduling;
using Behind.Contact.Domain;
using Behind.Contact.WebApi;
using Behind.Core.Domain;
using Behind.Web.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class JsonSchemaPostgresIntegrationTests
{
    [Fact]
    public async Task ValidatedContactAndScheduleJson_RoundTripsThroughRealPostgresConverters()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured in postgresql.testsettings.json.");
        await using var postgres = new PostgreSqlBuilder(image).WithLabel("behind.test", "json-schema").Build();
        await postgres.StartAsync();
        var modules = ModuleCatalog.Create(
        [
            ContactModule.Descriptor, Behind.Quotes.WebApi.QuotesModule.Descriptor,
            Behind.Sport.WebApi.SportModule.Descriptor, Behind.Forum.WebApi.ForumModule.Descriptor
        ]).GetOrElse(ModuleCatalog.Empty);
        Assert.Contains(modules.Modules, module => module.Key == "contact");
        var options = new DbContextOptionsBuilder<WebContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var context = new WebContextPostgres(options, modules);
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync(Behind.Core.Database.RoleNameKeyMigration.CreatePostgresFunctionSql());
        await context.Database.EnsureCreatedAsync();
        var fields = ContactFieldSchema.From([new ContactField("agree", "Consent", ContactFieldType.Boolean, true, 0)]);
        var form = new ContactForm(new VersionedKey<Guid>(Guid.NewGuid(), 1), "Support", "Questions",
            "admin@example.org", ContactSubjectMode.Choices, ContactSubjects.From(["Support"]), fields);
        var schedule = new Schedule(new VersionedKey<Guid>(Guid.NewGuid(), 1), "Cleanup",
            CronExpression.TryParse("0 3 * * *").Match(
                () => throw new InvalidOperationException("Invalid test cron."), value => value),
            "core.session-cleanup", new ScheduleParameters(new Dictionary<string, string> { ["days"] = "30" }),
            true, ScheduleNotification.Never, ScheduleParticipants.Empty);
        context.ContactForms.Add(form);
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var storedForm = await context.ContactForms.SingleAsync();
        var storedSchedule = await context.Schedules.SingleAsync();
        Assert.Equal(fields.Items.ToArray(), storedForm.Fields.Items.ToArray());
        Assert.Equal(form.Subjects.Items.ToArray(), storedForm.Subjects.Items.ToArray());
        Assert.Equal(schedule.Parameters, storedSchedule.Parameters);
        Assert.True(JsonSchemas.IsValid("contact-fields-1.0.0", JsonSerializer.SerializeToNode(storedForm.Fields)));
        Assert.True(JsonSchemas.IsValid("schedule-parameters-1.0.0", JsonNode.Parse(storedSchedule.Parameters.ToJson())));

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContactFieldSchema>("""[{"Type":"unknown"}]"""));
        Assert.Equal(1, await context.ContactForms.CountAsync());
    }
}
