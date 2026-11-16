using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Sport.Common;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Behind.Sport.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.Sport;

public class SportEndpointsTests
{
    private static readonly Guid Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IListingQueries<DisciplineListing> Queries()
    {
        var queries = Substitute.For<IListingQueries<DisciplineListing>>();
        queries.List(default!, default).ReturnsForAnyArgs(
            new PagedResult<DisciplineListing>(1, 1, 1, 10, Some<IEnumerable<DisciplineListing>>([new DisciplineListing(Id, "Soccer", "soccer", false)])));
        queries.GetLetters(default!, default).ReturnsForAnyArgs(["S"]);

        return queries;
    }

    [Fact]
    public async Task List_ReturnsThePageWithoutCachingAndPassesTheFilters()
    {
        var queries = Queries();
        var context = new DefaultHttpContext();
        var season = Guid.NewGuid();

        var result = await ResourceEndpoints.List(
            context,
            queries,
            new FakeAuthorizationService(),
            new SportListQuery(2, 5, " so ", "s", false, null, season, null, null, null, null, StatScope.Team));

        var body = Assert.IsType<JsonHttpResult<SportListResponse<DisciplineListing>>>(result).Value!;
        Assert.Equal(["S"], body.Letters);
        Assert.Equal("Soccer", Assert.Single(body.Page.Items).Name);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        await queries.Received(1).List(
            new ListRequest(
                new Flipper(2, 5),
                Some("so"),
                Some("S"),
                DeletionState.Active,
                SportFilter.Empty with { Season = season, Scope = StatScope.Team }),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPlayers_ExposesAgeButNotBirthDate()
    {
        var player = new PlayerListing(
            Id,
            Guid.NewGuid(),
            "Soccer",
            "Ada",
            "Lovelace",
            new DateOnly(2000, 12, 10),
            25,
            "British",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false);
        var queries = Substitute.For<IListingQueries<PlayerListing>>();
        queries.List(default!, default).ReturnsForAnyArgs(
            new PagedResult<PlayerListing>(1, 1, 1, 10, Some<IEnumerable<PlayerListing>>([player])));
        queries.GetLetters(default!, default).ReturnsForAnyArgs(["L"]);

        var result = await ResourceEndpoints.ListPlayers(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(),
            new SportListQuery());

        var body = Assert.IsType<JsonHttpResult<SportListResponse<PlayerPublicListing>>>(result).Value!;
        var publicPlayer = Assert.Single(body.Page.Items);
        Assert.Equal(25, publicPlayer.Age);
        Assert.DoesNotContain("birthDate", JsonSerializer.Serialize(body), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_IgnoresTheDeletedSwitchAndManageHonoursIt()
    {
        var queries = Queries();

        var listed = await ResourceEndpoints.List(new DefaultHttpContext(), queries, new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant), new SportListQuery(Deleted: true));
        var forbidden = await ResourceEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), new SportListQuery(Deleted: true));
        var allowed = await ResourceEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant), new SportListQuery(Deleted: true));

        Assert.IsType<JsonHttpResult<SportListResponse<DisciplineListing>>>(listed);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(forbidden).StatusCode);
        Assert.IsType<JsonHttpResult<SportListResponse<DisciplineListing>>>(allowed);
        await queries.Received(1).List(Arg.Is<ListRequest>(request => request.State == DeletionState.Deleted), Arg.Any<CancellationToken>());
        await queries.Received(1).List(Arg.Is<ListRequest>(request => request.State == DeletionState.Active), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_UnknownLetterIsABadRequest()
    {
        var result = await ResourceEndpoints.List(new DefaultHttpContext(), Queries(), new FakeAuthorizationService(), new SportListQuery(Letter: "ab"));

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Create_NormalizesTheDraftAndAnswersTheSavedVersion()
    {
        var commands = Substitute.For<IVersionedCommands<DisciplineDraft>>();
        commands.Create(default!, default).ReturnsForAnyArgs(new VersionedSaveResult.Saved(new VersionedKey<Guid>(Id, 1)));

        var result = await ResourceEndpoints.Create(new DisciplineDraft("  Soccer ", " SOCCER "), new DefaultHttpContext(), commands);

        Assert.Equal(new VersionedSavedResponse(Id, 1), Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(result).Value);
        await commands.Received(1).Create(new DisciplineDraft("Soccer", "soccer"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_MapsEveryOutcome()
    {
        var commands = Substitute.For<IVersionedCommands<DisciplineDraft>>();
        var draft = new DisciplineDraft("Soccer", "soccer");

        commands.Update(Id, draft, default).ReturnsForAnyArgs(new VersionedSaveResult.NotFound());
        Assert.IsType<NotFound>(await ResourceEndpoints.Update(Id, draft, new DefaultHttpContext(), commands));

        commands.Update(Id, draft, default).ReturnsForAnyArgs(new VersionedSaveResult.VersionConflict());
        var conflict = Assert.IsType<Conflict<VersionedConflictResponse>>(await ResourceEndpoints.Update(Id, draft, new DefaultHttpContext(), commands));
        Assert.Equal(VersionedSaveResultExtensions.VersionConflictReason, conflict.Value!.Reason);

        commands.Update(Id, draft, default).ReturnsForAnyArgs(new VersionedSaveResult.Rejected(SportRejections.Mismatch));
        var rejected = Assert.IsType<Conflict<VersionedConflictResponse>>(await ResourceEndpoints.Update(Id, draft, new DefaultHttpContext(), commands));
        Assert.Equal(SportRejections.Mismatch, rejected.Value!.Reason);
    }

    [Fact]
    public async Task DeleteRestoreAndHardDelete_MapTheirResults()
    {
        var commands = Substitute.For<IVersionedCommands<DisciplineDraft>>();
        commands.Delete(Id, default).ReturnsForAnyArgs(true);
        commands.Restore(Id, default).ReturnsForAnyArgs(false);
        commands.HardDelete(Id, default).ReturnsForAnyArgs(HardDeleteResult.Rejected);

        Assert.IsType<NoContent>(await ResourceEndpoints.Delete(Id, new DefaultHttpContext(), commands));
        Assert.IsType<NotFound>(await ResourceEndpoints.Restore(Id, new DefaultHttpContext(), commands));
        Assert.IsType<Conflict>(await ResourceEndpoints.HardDelete(Id, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task Details_AnswerFoundOrNotFound()
    {
        var queries = Substitute.For<ISportDetailQueries>();
        var discipline = new DisciplineDetail(new DisciplineListing(Id, "Soccer", "soccer", false), [], [], []);
        queries.Discipline(Id, default).ReturnsForAnyArgs(Some(discipline));
        queries.Team(Id, default).ReturnsForAnyArgs(Task.FromResult<Option<TeamDetail>>(None));

        Assert.Same(discipline, Assert.IsType<JsonHttpResult<DisciplineDetail>>(await DetailEndpoints.Discipline(Id, new DefaultHttpContext(), queries)).Value);
        Assert.IsType<NotFound>(await DetailEndpoints.Team(Id, new DefaultHttpContext(), queries));
    }

    [Theory]
    [InlineData(null, DetailEndpoints.DefaultLeaderboardSize)]
    [InlineData(0, 1)]
    [InlineData(1000, DetailEndpoints.MaxLeaderboardSize)]
    [InlineData(25, 25)]
    public async Task Leaderboards_ClampTheLimit(int? requested, int expected)
    {
        var queries = Substitute.For<ISportDetailQueries>();
        queries.Leaderboards(Id, expected, default).ReturnsForAnyArgs(Task.FromResult<Option<SeasonLeaderboards>>(None));
        queries.Leaderboard(Id, Id, expected, default).ReturnsForAnyArgs(Task.FromResult<Option<Leaderboard>>(None));

        await DetailEndpoints.Leaderboards(Id, new DefaultHttpContext(), queries, requested);
        await DetailEndpoints.Leaderboard(Id, Id, new DefaultHttpContext(), queries, requested);

        await queries.Received(1).Leaderboards(Id, expected, Arg.Any<CancellationToken>());
        await queries.Received(1).Leaderboard(Id, Id, expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Drafts_AreValidatedWithDataAnnotations()
    {
        bool Valid(object draft) => Validator.TryValidateObject(draft, new ValidationContext(draft), [], true);

        Assert.True(Valid(new DisciplineDraft("Soccer", "soccer-pro")));
        Assert.False(Valid(new DisciplineDraft("Soccer", "Not A Slug")));
        Assert.False(Valid(new DisciplineDraft("", "soccer")));
        Assert.False(Valid(new TeamDraft(Id, "T", null, null, null, 1500, null, null)));
        Assert.False(Valid(new PlayerDraft(Id, "A", "B", null, null, null, 10, null, null, null)));
        Assert.False(Valid(new PlayerSeasonDraft(Id, Id, Id, 1000, null)));
        Assert.True(Valid(new StatDraft(Id, Id, Id, 3.5)));
    }

    [Fact]
    public void Drafts_NormalizeOptionalText()
    {
        var team = new TeamDraft(Id, " T ", " ", " City ", null, null, "", null).Normalized();
        var player = new PlayerDraft(Id, " A ", " B ", null, " ", null, null, null, null, " bio ").Normalized();

        Assert.Equal(("T", null, "City", null), (team.Name, team.ShortName, team.City, team.Description));
        Assert.Equal(("A", "B", null, "bio"), (player.FirstName, player.LastName, player.Nationality, player.Biography));
        Assert.Equal(new PlayerSeasonDraft(Id, Id, Id, 1, null), new PlayerSeasonDraft(Id, Id, Id, 1, null).Normalized());
        Assert.Equal(new StatDraft(Id, Id, Id, 1), new StatDraft(Id, Id, Id, 1).Normalized());
        Assert.Equal(new SeasonDraft(Id, "S", null, null), new SeasonDraft(Id, " S ", null, null).Normalized());
        Assert.Equal(new PositionDraft(Id, "P", "PP", 1), new PositionDraft(Id, " P ", " PP ", 1).Normalized());
        Assert.Equal(new StatTypeDraft(Id, "P", "PP", StatScope.Team, true), new StatTypeDraft(Id, " P ", " PP ", StatScope.Team, true).Normalized());
    }

    private static IFormFile Form(byte[] content, string name = "pic.png")
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(name);
        file.Length.Returns(content.LongLength);
        file.OpenReadStream().Returns(_ => new MemoryStream(content));

        return file;
    }

    [Fact]
    public async Task UploadTeamLogo_StoresTheImageAndAnswersItsOwnUrl()
    {
        var files = new FakeModuleFiles();
        var png = TestImages.Png(4, 4);

        var result = await SportImageEndpoints.UploadTeamLogo(Form(png), new DefaultHttpContext(), files);

        var body = Assert.IsType<JsonHttpResult<ImageUploadResponse>>(result).Value!;
        Assert.Equal($"/sport/teams/images/{body.Id}", body.Url);
        Assert.True(await files.Exists(SportFileTypes.Teams, body.Id));
        Assert.False(await files.Exists(SportFileTypes.Players, body.Id));
    }

    [Fact]
    public async Task UploadPlayerPicture_AnswersThePlayerImageUrl()
    {
        var files = new FakeModuleFiles();

        var result = await SportImageEndpoints.UploadPlayerPicture(Form(TestImages.Jpeg(4, 4), "a.jpg"), new DefaultHttpContext(), files);

        var body = Assert.IsType<JsonHttpResult<ImageUploadResponse>>(result).Value!;
        Assert.Equal($"/sport/players/images/{body.Id}", body.Url);
        Assert.True(await files.Exists(SportFileTypes.Players, body.Id));
    }

    [Fact]
    public async Task Upload_RejectsMissingOversizedAndNonImageFiles()
    {
        var files = new FakeModuleFiles();
        var oversized = Substitute.For<IFormFile>();
        oversized.Length.Returns(SportModuleNames.ImageLimit + 1);

        Assert.IsType<BadRequest>(await SportImageEndpoints.UploadTeamLogo(null, new DefaultHttpContext(), files));
        Assert.IsType<BadRequest>(await SportImageEndpoints.UploadTeamLogo(Form([]), new DefaultHttpContext(), files));
        var tooLarge = await SportImageEndpoints.UploadTeamLogo(oversized, new DefaultHttpContext(), files);
        Assert.Equal(new ImageLimitResponse(SportModuleNames.ImageLimit), Assert.IsType<JsonHttpResult<ImageLimitResponse>>(tooLarge).Value);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await SportImageEndpoints.UploadTeamLogo(Form([1, 2, 3]), new DefaultHttpContext(), files)).StatusCode);
    }

    [Fact]
    public async Task Images_AreServedOnlyUnderTheirOwnKindOfUrl()
    {
        var files = new FakeModuleFiles();
        var logo = await files.Add(new IModuleFiles.Upload(SportFileTypes.Teams, "l.png", "image/png", TestImages.Png(2, 2)));
        var context = new DefaultHttpContext();

        var served = await SportImageEndpoints.GetTeamLogo(logo, context, files);

        Assert.IsType<FileContentHttpResult>(served);
        Assert.Equal(ImageResponseExtensions.ImmutableCache, context.Response.Headers.CacheControl);
        Assert.IsType<NotFound>(await SportImageEndpoints.GetPlayerPicture(logo, new DefaultHttpContext(), files));
        Assert.IsType<NotFound>(await SportImageEndpoints.GetTeamLogo(Guid.NewGuid(), new DefaultHttpContext(), files));
    }
}
