using Behind.Common.Database.Modules;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Quotes.WebApi;
using Behind.Sport.Database;
using Behind.Sport.Database.Commands;
using Behind.Sport.Database.Queries;
using Behind.Sport.Domain;
using Behind.Sport.WebApi;
using Behind.Web.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Helpers;

public sealed class SportTestBed : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly ModuleCatalog Catalog =
        ModuleCatalog.Create([QuotesModule.Descriptor, SportModule.Descriptor, Behind.Forum.WebApi.ForumModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SportTestBed()
    {
        _connection.Open();
        using var context = Read();
        context.Database.EnsureCreated();
        context.EnsureBuiltInUsersAsync().GetAwaiter().GetResult();
    }

    public FakeModuleFiles Files { get; } = new();

    public WebContextSqlite Read() =>
        new(new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite(_connection).Options, Catalog);

    public WebContextSqlite Write() =>
        new(
            new DbContextOptionsBuilder<WebContextSqlite>()
                .UseSqlite(_connection)
                .AddInterceptors(new AuditSaveChangesInterceptor(
                    () => new FixedAuditActorProvider(AuditActor.Autonomous),
                    new FixedTimeProvider(Now)))
                .Options,
            Catalog);

    public async Task<TResult> WithCommandsAsync<TDraft, TResult>(
        Func<IVersionedCommands<TDraft>, Task<TResult>> action)
    {
        await using var context = Write();

        return await action(CommandsFor<TDraft>(context));
    }

    public async Task<TResult> WithQueriesAsync<TListing, TResult>(
        Func<IListingQueries<TListing>, Task<TResult>> action)
    {
        await using var context = Read();

        return await action(QueriesFor<TListing>(context));
    }

    public async Task<TResult> WithDetailsAsync<TResult>(Func<ISportDetailQueries, Task<TResult>> action)
    {
        await using var context = Read();

        return await action(new SportDetailQueries(
            context,
            new DisciplineQueries(context),
            new PositionQueries(context),
            new StatTypeQueries(context),
            new SeasonQueries(context),
            new TeamQueries(context),
            new PlayerQueries(context, new FixedTimeProvider(Now)),
            new PlayerSeasonQueries(context),
            new StatQueries(context)));
    }

    public async Task<Guid> CreateAsync<TDraft>(TDraft draft)
    {
        var result = await WithCommandsAsync<TDraft, VersionedSaveResult>(commands => commands.Create(draft));

        return Assert.IsType<VersionedSaveResult.Saved>(result).Id.Key;
    }

    public Task<Guid> CreateDisciplineAsync(string name = "Soccer", string slug = "soccer") =>
        CreateAsync(new DisciplineDraft(name, slug));

    public Task<Guid> CreatePositionAsync(Guid discipline, string name = "Goalkeeper") =>
        CreateAsync(new PositionDraft(discipline, name, "GK", 1));

    public Task<Guid> CreateStatTypeAsync(
        Guid discipline,
        string name = "Goals",
        StatScope scope = StatScope.Player,
        bool higherIsBetter = true) =>
        CreateAsync(new StatTypeDraft(discipline, name, "G", scope, higherIsBetter));

    public Task<Guid> CreateSeasonAsync(
        Guid discipline,
        string name = "2025/26",
        DateOnly? startsOn = null) =>
        CreateAsync(new SeasonDraft(discipline, name, startsOn ?? new DateOnly(2025, 8, 1), null));

    public Task<Guid> CreateTeamAsync(Guid discipline, string name = "Rovers", Guid? logo = null) =>
        CreateAsync(new TeamDraft(discipline, name, null, "Town", "England", 1900, null, logo));

    public Task<Guid> CreatePlayerAsync(
        Guid discipline,
        string firstName = "Ada",
        string lastName = "Lovelace",
        Guid? position = null) =>
        CreateAsync(new PlayerDraft(
            discipline,
            firstName,
            lastName,
            new DateOnly(2000, 12, 10),
            "British",
            position,
            170,
            65,
            Side.Right,
            null));

    public Task<Guid> CreateMembershipAsync(
        Guid player,
        Guid season,
        Guid team,
        int? shirt = 9,
        Guid? picture = null) =>
        CreateAsync(new PlayerSeasonDraft(player, season, team, shirt, picture));

    public Task<Guid> CreateStatAsync(Guid season, Guid statType, Guid subject, double value) =>
        CreateAsync(new StatDraft(season, statType, subject, value));

    public async Task<IReadOnlyList<TListing>> ListAsync<TListing>(
        DeletionState state = DeletionState.Active,
        SportFilter? filter = null,
        Option<string> search = default,
        Option<string> letter = default)
    {
        var page = await WithQueriesAsync<TListing, PagedResult<TListing>>(queries => queries.List(
            new ListRequest(new Flipper(1, 100), search, letter, state, filter ?? SportFilter.Empty)));

        return page.Results.Match(() => (IReadOnlyList<TListing>)[], items => items.ToList());
    }

    private IVersionedCommands<TDraft> CommandsFor<TDraft>(WebContextSqlite context) =>
        (IVersionedCommands<TDraft>)(object)(typeof(TDraft).Name switch
        {
            nameof(DisciplineDraft) => new DisciplineCommands(context),
            nameof(PositionDraft) => new PositionCommands(context),
            nameof(StatTypeDraft) => new StatTypeCommands(context),
            nameof(SeasonDraft) => new SeasonCommands(context),
            nameof(TeamDraft) => new TeamCommands(context, Files),
            nameof(PlayerDraft) => new PlayerCommands(context),
            nameof(PlayerSeasonDraft) => new PlayerSeasonCommands(context, Files),
            nameof(StatDraft) => new StatCommands(context),
            _ => throw new InvalidOperationException(typeof(TDraft).Name)
        });

    private static IListingQueries<TListing> QueriesFor<TListing>(WebContextSqlite context) =>
        (IListingQueries<TListing>)(object)(typeof(TListing).Name switch
        {
            nameof(DisciplineListing) => new DisciplineQueries(context),
            nameof(PositionListing) => new PositionQueries(context),
            nameof(StatTypeListing) => new StatTypeQueries(context),
            nameof(SeasonListing) => new SeasonQueries(context),
            nameof(TeamListing) => new TeamQueries(context),
            nameof(PlayerListing) => new PlayerQueries(context, new FixedTimeProvider(Now)),
            nameof(PlayerSeasonListing) => new PlayerSeasonQueries(context),
            nameof(StatListing) => new StatQueries(context),
            _ => throw new InvalidOperationException(typeof(TListing).Name)
        });

    public void Dispose() => _connection.Dispose();
}
