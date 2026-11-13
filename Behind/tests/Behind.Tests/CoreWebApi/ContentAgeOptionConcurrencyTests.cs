using System.Data;
using System.Data.Common;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentAgeOptionConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDeleteAndRestrictionSavePreservesAnySuccessfulAssignment(bool saveFirst)
    {
        var connectionString = $"Data Source=age-options-{Guid.NewGuid()};Mode=Memory;Cache=Shared;Default Timeout=5";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        Guid optionId;
        await using (var seed = CreateContext(connectionString))
        {
            await seed.Database.EnsureCreatedAsync();
            var created = Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
                await ContentAgeOptionEndpoints.Create(new(18, "18"), new DefaultHttpContext(), seed));
            optionId = created.Value!.Id;
        }

        var blocker = new BlockingSaveInterceptor();
        var started = new TransactionStartingInterceptor();
        await using var first = CreateContext(connectionString, blocker);
        await using var second = CreateContext(connectionString, started);
        var firstTask = Task.Run(() => saveFirst ? SaveRestriction(first) : DeleteOption(first));
        await blocker.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondTask = Task.Run(() => saveFirst ? DeleteOption(second) : SaveRestriction(second));
        try
        {
            await started.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            blocker.Release.TrySetResult();
        }
        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.All(results, result => Assert.True(result is NoContent or BadRequest or Conflict));
        await using var read = CreateContext(connectionString);
        var restrictions = await read.ContentRestrictions.LatestOf(read.ContentRestrictions).ToListAsync();
        var saved = results[saveFirst ? 0 : 1] is NoContent;
        var deleted = results[saveFirst ? 1 : 0] is NoContent;
        if (saved)
        {
            Assert.Equal(18, Assert.Single(restrictions).MinimumAge);
        }
        else
        {
            Assert.Empty(restrictions);
        }
        if (deleted)
        {
            Assert.False(await read.IsActiveContentAgeOption(18));
        }

        Task<IResult> DeleteOption(AbstractCoreContext context) =>
            ContentAgeOptionEndpoints.Delete(optionId, new DefaultHttpContext(), context);
    }

    [Fact]
    public async Task ConcurrentCreatesCannotPersistDuplicateActiveAges()
    {
        var connectionString = $"Data Source=age-options-{Guid.NewGuid()};Mode=Memory;Cache=Shared;Default Timeout=5";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        await using (var seed = CreateContext(connectionString))
        {
            await seed.Database.EnsureCreatedAsync();
        }
        var blocker = new BlockingSaveInterceptor();
        var started = new TransactionStartingInterceptor();
        await using var first = CreateContext(connectionString, blocker);
        await using var second = CreateContext(connectionString, started);
        var firstTask = Task.Run(() => ContentAgeOptionEndpoints.Create(new(18, "18"), new DefaultHttpContext(), first));
        await blocker.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondTask = Task.Run(() => ContentAgeOptionEndpoints.Create(new(18, "18"), new DefaultHttpContext(), second));
        try
        {
            await started.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            blocker.Release.TrySetResult();
        }
        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Single(results, result => result is Created<ContentAgeOptionEndpoints.AgeOptionResponse>);
        Assert.Single(results, result => result is Conflict);
        await using var read = CreateContext(connectionString);
        Assert.Single(await read.ActiveContentAgeOptions.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentUpdateAndDeleteCannotRestoreADeletedOption(bool updateFirst)
    {
        var connectionString = $"Data Source=age-options-{Guid.NewGuid()};Mode=Memory;Cache=Shared;Default Timeout=5";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        Guid id;
        await using (var seed = CreateContext(connectionString))
        {
            await seed.Database.EnsureCreatedAsync();
            var created = Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
                await ContentAgeOptionEndpoints.Create(new(18, "Original"), new DefaultHttpContext(), seed));
            id = created.Value!.Id;
        }

        var blocker = new BlockingSaveInterceptor();
        var started = new TransactionStartingInterceptor();
        await using var first = CreateContext(connectionString, blocker);
        await using var second = CreateContext(connectionString, started);
        Task<IResult> Update(AbstractCoreContext context) =>
            ContentAgeOptionEndpoints.Update(id, new("Renamed"), new DefaultHttpContext(), context);
        Task<IResult> Delete(AbstractCoreContext context) =>
            ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context);
        var firstTask = Task.Run(() => updateFirst ? Update(first) : Delete(first));
        await blocker.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondTask = Task.Run(() => updateFirst ? Delete(second) : Update(second));
        try
        {
            await started.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            blocker.Release.TrySetResult();
        }
        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.All(results, result => Assert.True(result is Ok<ContentAgeOptionEndpoints.AgeOptionResponse> or NoContent or Conflict or NotFound));
        await using var read = CreateContext(connectionString);
        if (results.Any(result => result is NoContent))
        {
            Assert.False(await read.IsActiveContentAgeOption(18));
        }
        Assert.All(await read.ContentAgeOptions.IgnoreQueryFilters().ToListAsync(), item => Assert.Equal(18, item.Age));
    }

    private static Task<IResult> SaveRestriction(AbstractCoreContext context) =>
        ContentAgeOptionEndpoints.ExecuteMutation(context, CancellationToken.None, async () =>
        {
            Assert.Equal(IsolationLevel.Serializable,
                context.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            if (!await context.IsActiveContentAgeOption(18))
            {
                return Results.BadRequest();
            }
            var key = Guid.NewGuid();
            var restriction = new ContentRestriction(new VersionedKey<Guid>(key, 1), nameof(News), key, 18);
            context.ContentRestrictions.Add(restriction);
            context.Entry(restriction).StampVersionedKey(key, 1);
            await context.SaveChangesAsync();
            return Results.NoContent();
        });

    private static CoreContextSqlite CreateContext(string connectionString, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite(connectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(
                () => new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System));
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }
        return new CoreContextSqlite(options.Options);
    }

    private sealed class BlockingSaveInterceptor : SaveChangesInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class TransactionStartingInterceptor : DbTransactionInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
