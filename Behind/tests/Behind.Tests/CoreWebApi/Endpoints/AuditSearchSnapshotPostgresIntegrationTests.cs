using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreWebApi.Endpoints;

[Trait("Category", "Integration")]
public sealed class AuditSearchSnapshotPostgresIntegrationTests
{
    [Fact]
    public async Task InsertBetweenBatchesPreservesEveryOriginalMatchExactlyOnce()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres image required.")).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var writer = new CoreContextPostgres(options);
        await writer.Database.EnsureCreatedAsync();
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var batchSize = AuditLogEntryEndpoints.MaxPageSize;
        for (var index = 0; index <= batchSize; index++)
        {
            var type = index < batchSize - 1 ? "Other" : "Target";
            writer.AuditLogEntries.Add(new AuditLogEntry(type, index.ToString(), new AuditLogEntry.Created(),
                now.AddSeconds(-index)));
        }
        await writer.SaveChangesAsync();

        await using var reader = new CoreContextPostgres(options);
        var real = new AuditLogEntryQueries(reader, []);
        var queries = Substitute.For<IAuditLogEntryQueries>();
        queries.BeginReadSnapshot(Arg.Any<CancellationToken>())
            .Returns(call => real.BeginReadSnapshot(call.Arg<CancellationToken>()));
        queries.ResolveLabels(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => real.ResolveLabels(call.Arg<IReadOnlyCollection<Guid>>(), call.Arg<CancellationToken>()));
        var batches = new List<int>();
        queries.GetAuditLog(Arg.Any<IAuditLogEntryQueries.RequestAuditLog>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var request = call.Arg<IAuditLogEntryQueries.RequestAuditLog>();
                var page = await real.GetAuditLog(request, call.Arg<CancellationToken>());
                batches.Add(request.Flipper.Index);
                if (request.Flipper.Index == 1)
                {
                    writer.AuditLogEntries.Add(new AuditLogEntry("Target", "inserted", new AuditLogEntry.Created(),
                        now.AddSeconds(1)));
                    await writer.SaveChangesAsync();
                }
                return page;
            });

        var result = await AuditLogEntryEndpoints.Get(new DefaultHttpContext(), queries, new FixedTimeProvider(now),
            entity: "Target", entityExact: true);
        var response = Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!;
        Assert.Equal([1, 2], batches);
        Assert.Equal(2, response.Total);
        Assert.Equal([(batchSize - 1).ToString(), batchSize.ToString()], response.Items.Select(item => item.EntityKey));
    }
}
