using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class ReportStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static UserReport Report(Guid id, string reason = "Private reason") =>
        new(new(id, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), reason,
            "chatMessage", Guid.NewGuid(), Guid.NewGuid(), "Private evidence", "open", null, "", "", "");

    [Fact]
    public async Task SavePreservesEvidenceAndAppendsResolutionVersions()
    {
        var id = Guid.NewGuid();
        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            var store = new ReportStore(context);
            var report = Report(id);
            await store.Save(report, default);
            await store.Save(report with { Id = new(id, 2), Status = "resolved", Resolution = "resolve" }, default);
        }

        await using var read = _factory.CreateContext();
        var current = await new ReportStore(read).Find(id, false, default);
        Assert.NotNull(current);
        Assert.Equal(2, current.Id.Version);
        Assert.Equal("Private evidence", current.EvidenceContent);
        Assert.Equal("resolved", current.Status);
        Assert.Equal(2, await read.UserReports.OfKey(id).CountAsync());
        var times = await read.AuditTimesAsync<UserReport>(new Dictionary<Guid, long> { [id] = 2 });
        Assert.True(times[id].CreatedAt.Match(() => false, _ => true));
        Assert.True(times[id].UpdatedAt.Match(() => false, _ => true));
    }

    [Fact]
    public async Task PagingAndStatusFilteringUseOnlyLatestVersions()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var store = new ReportStore(context);
        for (var index = 0; index < 5; index++)
        {
            await store.Save(Report(Guid.NewGuid()), default);
        }

        var first = await store.List("open", 1, 2, false, default);
        var second = await store.List("open", 2, 2, false, default);
        Assert.Equal(5, first.Total);
        Assert.Equal(3, first.PageCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Empty(first.Items.Select(item => item.Id.Key).Intersect(second.Items.Select(item => item.Id.Key)));
        var resolved = first.Items[0] with { Id = new(first.Items[0].Id.Key, 2), Status = "resolved" };
        await store.Save(resolved, default);
        Assert.Equal(4, (await store.List("open", 1, 20, false, default)).Total);
        Assert.Equal(1, (await store.List("resolved", 1, 20, false, default)).Total);
    }

    [Fact]
    public async Task SoftDeleteRestoreAndHardDeleteFollowAuditState()
    {
        var id = Guid.NewGuid();
        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new ReportStore(context).Save(Report(id), default);
        }

        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            Assert.True(await new ReportStore(context).Delete(id, "delete", default));
        }

        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            var store = new ReportStore(context);
            Assert.Null(await store.Find(id, false, default));
            Assert.NotNull(await store.Find(id, true, default));
            Assert.True(await store.Delete(id, "restore", default));
        }

        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            var store = new ReportStore(context);
            Assert.NotNull(await store.Find(id, false, default));
            Assert.True(await store.Delete(id, "delete", default));
        }

        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            Assert.True(await new ReportStore(context).Delete(id, "hard/delete", default));
        }

        await using var read = _factory.CreateContext();
        using var scope = read.SoftDelete.IncludeDeleted();
        Assert.Empty(await read.UserReports.OfKey(id).ToListAsync());
    }

    [Fact]
    public async Task AuditLabelsNeverExposePrivateReasonsEvidenceOrEmail()
    {
        var reportId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var store = new ReportStore(context);
        store.StageDelivery(new(new(deliveryId, 1), reportId, Guid.NewGuid(), "private@example.test", "Secret", "pending"));
        await store.Save(Report(reportId), default);
        var labels = await context.ResolveAuditLabelsAsync([reportId, deliveryId], []);
        Assert.Equal("User report", labels.Label(reportId.ToString()));
        Assert.Equal("Report notification", labels.Label(deliveryId.ToString()));
    }
}
