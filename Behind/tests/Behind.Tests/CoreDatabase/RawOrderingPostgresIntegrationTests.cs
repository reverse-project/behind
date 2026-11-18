using Behind.Common;
using Behind.Common.Database.Raw;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class RawOrderingPostgresIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SensitiveValuesNeverControlOrdering(bool sensitiveKey)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Missing PostgreSQL image");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        await using var context = new DbContext(new DbContextOptionsBuilder().UseNpgsql(postgres.GetConnectionString()).Options);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "SecretProbe" ("Id" integer, "Secret" text);
            INSERT INTO "SecretProbe" VALUES (1, 'z'), (2, 'a');
            """);
        var table = new RawTable("SecretProbe", null, "SecretProbe",
        [
            new("Id", "integer", RawValueKind.Integer, false, true, sensitiveKey),
            new("Secret", "text", RawValueKind.Text, false, false, true)
        ]);
        var result = await context.ListRawRowsAsync(table, new(new(1, 10), None, Some("Secret"), false));
        var rows = result.Results.GetOrElse([]).ToArray();
        if (!sensitiveKey)
        {
            Assert.Equal(new object?[] { 1, 2 }, rows.Select(row => row.Values["Id"]));
        }
        else
        {
            Assert.Equal(2, rows.Length);
        }
    }
}
