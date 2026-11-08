using Behind.Sport.Database;
using Behind.Sport.Domain;

namespace Behind.Tests.Sport;

public sealed class SportQueriesTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private sealed record World(
        Guid Soccer,
        Guid Hockey,
        Guid Season,
        Guid OldSeason,
        Guid Rovers,
        Guid United,
        Guid Goalie,
        Guid Ada,
        Guid Bob,
        Guid Goals,
        Guid Wins);

    private async Task<World> BuildAsync()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var hockey = await _bed.CreateDisciplineAsync("Hockey", "hockey");
        var season = await _bed.CreateSeasonAsync(soccer);
        var oldSeason = await _bed.CreateSeasonAsync(soccer, "2024/25", new DateOnly(2024, 8, 1));
        var rovers = await _bed.CreateTeamAsync(soccer);
        var united = await _bed.CreateTeamAsync(soccer, "United");
        var goalie = await _bed.CreatePositionAsync(soccer);
        var ada = await _bed.CreatePlayerAsync(soccer, "Ada", "Lovelace", goalie);
        var bob = await _bed.CreatePlayerAsync(soccer, "Bob", "Zed");
        var goals = await _bed.CreateStatTypeAsync(soccer);
        var wins = await _bed.CreateStatTypeAsync(soccer, "Wins", StatScope.Team);
        await _bed.CreateStatTypeAsync(hockey, "Saves");

        await _bed.CreateMembershipAsync(ada, oldSeason, united, 7);
        await _bed.CreateMembershipAsync(ada, season, rovers, 9);
        await _bed.CreateMembershipAsync(bob, season, united, 11);
        await _bed.CreateStatAsync(season, goals, ada, 12);
        await _bed.CreateStatAsync(season, goals, bob, 12);
        await _bed.CreateStatAsync(oldSeason, goals, ada, 3);
        await _bed.CreateStatAsync(season, wins, rovers, 20);

        return new World(soccer, hockey, season, oldSeason, rovers, united, goalie, ada, bob, goals, wins);
    }

    [Fact]
    public async Task Players_ShowAgePositionAndTheTeamOfTheNewestSeason()
    {
        var world = await BuildAsync();

        var players = await _bed.ListAsync<PlayerListing>();

        Assert.Equal(["Lovelace", "Zed"], players.Select(player => player.LastName));
        var ada = players[0];
        Assert.Equal(25, ada.Age);
        Assert.Equal("Goalkeeper", ada.PositionName);
        Assert.Equal((world.Rovers, "Rovers", "Soccer"), (ada.TeamId, ada.TeamName, ada.DisciplineName));
        Assert.Equal("Ada Lovelace", ada.Name);
    }

    [Fact]
    public async Task Players_AreFilteredBySportTeamSeasonPositionAndSearch()
    {
        var world = await BuildAsync();

        Assert.Equal(["Ada", "Bob"], (await _bed.ListAsync<PlayerListing>(filter: SportFilter.Empty with { Discipline = world.Soccer })).Select(row => row.FirstName));
        Assert.Empty(await _bed.ListAsync<PlayerListing>(filter: SportFilter.Empty with { Discipline = world.Hockey }));
        Assert.Equal(["Bob"], (await _bed.ListAsync<PlayerListing>(filter: SportFilter.Empty with { Team = world.United, Season = world.Season })).Select(row => row.FirstName));
        Assert.Equal(["Ada"], (await _bed.ListAsync<PlayerListing>(filter: SportFilter.Empty with { Position = world.Goalie })).Select(row => row.FirstName));
        Assert.Equal(["Bob"], (await _bed.ListAsync<PlayerListing>(search: "ZED")).Select(row => row.FirstName));
        Assert.Equal(["Z"], await _bed.WithQueriesAsync<PlayerListing, IReadOnlyList<string>>(queries => queries.GetLetters(new ListRequest(new Flipper(1, 10), "bob", None, DeletionState.Active, SportFilter.Empty))));
        Assert.Equal(["Ada"], (await _bed.ListAsync<PlayerListing>(letter: "L")).Select(row => row.FirstName));
    }

    [Fact]
    public async Task Teams_AreFilteredByTheSeasonTheyPlayed()
    {
        var world = await BuildAsync();

        var teams = await _bed.ListAsync<TeamListing>(filter: SportFilter.Empty with { Season = world.OldSeason });

        Assert.Equal(["United"], teams.Select(team => team.Name));
        Assert.Equal(["Rovers", "United"], (await _bed.ListAsync<TeamListing>(search: "o")).Select(team => team.Name).Order());
    }

    [Fact]
    public async Task Memberships_StatsAndSeasonsAreFiltered()
    {
        var world = await BuildAsync();

        Assert.Equal(2, (await _bed.ListAsync<PlayerSeasonListing>(filter: SportFilter.Empty with { Player = world.Ada })).Count);
        Assert.Single(await _bed.ListAsync<PlayerSeasonListing>(filter: SportFilter.Empty with { Team = world.Rovers }));
        Assert.Single(await _bed.ListAsync<PlayerSeasonListing>(search: "bob"));
        Assert.Equal(3, (await _bed.ListAsync<StatListing>(filter: SportFilter.Empty with { Season = world.Season })).Count);
        Assert.Equal(["Wins"], (await _bed.ListAsync<StatListing>(filter: SportFilter.Empty with { Scope = StatScope.Team })).Select(row => row.StatTypeName));
        Assert.Equal(2, (await _bed.ListAsync<StatListing>(filter: SportFilter.Empty with { Player = world.Ada })).Count);
        Assert.Equal(["Rovers"], (await _bed.ListAsync<StatListing>(search: "rovers")).Select(row => row.SubjectName));
        Assert.Equal(4, (await _bed.ListAsync<StatListing>(filter: SportFilter.Empty with { Discipline = world.Soccer })).Count);
        Assert.Equal(2, (await _bed.ListAsync<SeasonListing>(filter: SportFilter.Empty with { Discipline = world.Soccer })).Count);
        Assert.Equal(["2025/26", "2024/25"], (await _bed.ListAsync<SeasonListing>()).Select(row => row.Name));
        Assert.Equal(2, (await _bed.ListAsync<StatTypeListing>(filter: SportFilter.Empty with { Discipline = world.Soccer })).Count);
        Assert.Equal(["Goalkeeper"], (await _bed.ListAsync<PositionListing>(filter: SportFilter.Empty with { Discipline = world.Soccer })).Select(row => row.Name));
        Assert.Equal(["Hockey", "Soccer"], (await _bed.ListAsync<DisciplineListing>()).Select(row => row.Name));
    }

    [Fact]
    public async Task Find_ReturnsTheLatestVersionOrNone()
    {
        var world = await BuildAsync();
        await _bed.WithCommandsAsync<TeamDraft, VersionedSaveResult>(commands => commands.Update(world.Rovers, new TeamDraft(world.Soccer, "Rovers FC", null, null, null, null, null, null)));

        var found = await _bed.WithQueriesAsync<TeamListing, Option<TeamListing>>(queries => queries.Find(world.Rovers));
        var missing = await _bed.WithQueriesAsync<TeamListing, Option<TeamListing>>(queries => queries.Find(Guid.NewGuid()));

        Assert.Equal("Rovers FC", found.Match(() => string.Empty, team => team.Name));
        Assert.True(missing.IsNone());
    }

    [Fact]
    public async Task DeletedRowsAreListedOnlyInTheDeletedState()
    {
        var world = await BuildAsync();
        await _bed.WithCommandsAsync<PlayerDraft, bool>(commands => commands.Delete(world.Bob));

        Assert.Equal(["Ada"], (await _bed.ListAsync<PlayerListing>()).Select(row => row.FirstName));
        var deleted = Assert.Single(await _bed.ListAsync<PlayerListing>(DeletionState.Deleted));
        Assert.Equal((world.Bob, true), (deleted.Id, deleted.IsDeleted));
    }

    [Fact]
    public async Task PlayerDetail_ShowsEverySeasonWithItsOwnPictureTeamAndStats()
    {
        var world = await BuildAsync();

        var detail = (await _bed.WithDetailsAsync(queries => queries.Player(world.Ada))).Match(() => null!, found => found);

        Assert.Equal(["2025/26", "2024/25"], detail.Seasons.Select(season => season.Season.Name));
        Assert.Equal(["Rovers", "United"], detail.Seasons.Select(season => season.Membership!.TeamName));
        Assert.Equal([12d, 3d], detail.Seasons.Select(season => Assert.Single(season.Stats).Value));
    }

    [Fact]
    public async Task TeamDetail_GroupsTheRosterAndStatsBySeason()
    {
        var world = await BuildAsync();

        var detail = (await _bed.WithDetailsAsync(queries => queries.Team(world.Rovers))).Match(() => null!, found => found);

        var season = Assert.Single(detail.Seasons);
        Assert.Equal(world.Season, season.Season.Id);
        Assert.Equal(["Ada Lovelace"], season.Players.Select(player => player.PlayerName));
        Assert.Equal(["Wins"], season.Stats.Select(stat => stat.StatTypeName));
    }

    [Fact]
    public async Task SeasonAndDisciplineDetails_ListTheirTeamsAndCatalog()
    {
        var world = await BuildAsync();

        var season = (await _bed.WithDetailsAsync(queries => queries.Season(world.Season))).Match(() => null!, found => found);
        var discipline = (await _bed.WithDetailsAsync(queries => queries.Discipline(world.Soccer))).Match(() => null!, found => found);

        Assert.Equal([("Rovers", 1), ("United", 1)], season.Teams.Select(team => (team.Name, team.PlayerCount)));
        Assert.Equal((1, 2, 2), (discipline.Positions.Count, discipline.StatTypes.Count, discipline.Seasons.Count));
    }

    [Fact]
    public async Task DetailsOfUnknownEntitiesAreNone()
    {
        await BuildAsync();
        var unknown = Guid.NewGuid();

        Assert.True((await _bed.WithDetailsAsync(queries => queries.Team(unknown))).IsNone());
        Assert.True((await _bed.WithDetailsAsync(queries => queries.Player(unknown))).IsNone());
        Assert.True((await _bed.WithDetailsAsync(queries => queries.Season(unknown))).IsNone());
        Assert.True((await _bed.WithDetailsAsync(queries => queries.Discipline(unknown))).IsNone());
        Assert.True((await _bed.WithDetailsAsync(queries => queries.Leaderboards(unknown, 10))).IsNone());
    }

    [Fact]
    public async Task Leaderboard_RanksTiesEquallyAndLinksPlayersToTheirTeam()
    {
        var world = await BuildAsync();

        var board = (await _bed.WithDetailsAsync(queries => queries.Leaderboard(world.Season, world.Goals, 10))).Match(() => null!, found => found);

        Assert.Equal([1, 1], board.Entries.Select(entry => entry.Rank));
        Assert.Equal(["Ada Lovelace", "Bob Zed"], board.Entries.Select(entry => entry.SubjectName).Order());
        Assert.Equal(["Rovers", "United"], board.Entries.Select(entry => entry.TeamName).Order());
    }

    [Fact]
    public async Task Leaderboard_OrdersLowerIsBetterAscendingAndHonoursTheLimit()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var season = await _bed.CreateSeasonAsync(soccer);
        var conceded = await _bed.CreateStatTypeAsync(soccer, "Conceded", StatScope.Team, false);
        var first = await _bed.CreateTeamAsync(soccer, "First");
        var second = await _bed.CreateTeamAsync(soccer, "Second");
        await _bed.CreateStatAsync(season, conceded, first, 30);
        await _bed.CreateStatAsync(season, conceded, second, 10);

        var board = (await _bed.WithDetailsAsync(queries => queries.Leaderboard(season, conceded, 1))).Match(() => null!, found => found);

        Assert.Equal(["Second"], board.Entries.Select(entry => entry.SubjectName));
        Assert.Equal(StatScope.Team, board.Entries[0].Scope);
    }

    [Fact]
    public async Task Leaderboard_OfAStatTypeFromAnotherSportIsNone()
    {
        var world = await BuildAsync();
        var saves = (await _bed.ListAsync<StatTypeListing>(filter: SportFilter.Empty with { Discipline = world.Hockey })).Single().Id;

        Assert.True((await _bed.WithDetailsAsync(queries => queries.Leaderboard(world.Season, saves, 10))).IsNone());
    }

    [Fact]
    public async Task Leaderboards_ListOnlyTheBoardsThatHaveEntries()
    {
        var world = await BuildAsync();

        var boards = (await _bed.WithDetailsAsync(queries => queries.Leaderboards(world.Season, 10))).Match(() => null!, found => found);

        Assert.Equal(["Goals", "Wins"], boards.Boards.Select(board => board.StatType.Name).Order());
    }
}
