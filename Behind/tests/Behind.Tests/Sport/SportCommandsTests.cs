using Behind.Common.Database.Modules;
using Behind.Sport.Database;
using Behind.Sport.Domain;

namespace Behind.Tests.Sport;

public sealed class SportCommandsTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static string Rejection(VersionedSaveResult result) => Assert.IsType<VersionedSaveResult.Rejected>(result).Reason;

    [Fact]
    public async Task Create_StoresAVersionedDisciplineAndUpdateAddsTheNextVersion()
    {
        var key = await _bed.CreateDisciplineAsync();

        var updated = await _bed.WithCommandsAsync<DisciplineDraft, VersionedSaveResult>(
            commands => commands.Update(key, new DisciplineDraft("Football", "soccer")));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<VersionedSaveResult.Saved>(updated).Id);
        Assert.Equal("Football", Assert.Single(await _bed.ListAsync<DisciplineListing>()).Name);
    }

    [Fact]
    public async Task Update_OfAnUnknownKeyIsNotFound()
    {
        var result = await _bed.WithCommandsAsync<DisciplineDraft, VersionedSaveResult>(
            commands => commands.Update(Guid.NewGuid(), new DisciplineDraft("Soccer", "soccer")));

        Assert.IsType<VersionedSaveResult.NotFound>(result);
    }

    [Fact]
    public async Task Discipline_RejectsADuplicateSlug()
    {
        await _bed.CreateDisciplineAsync();

        var result = await _bed.WithCommandsAsync<DisciplineDraft, VersionedSaveResult>(
            commands => commands.Create(new DisciplineDraft("Other", "soccer")));

        Assert.Equal(SportRejections.Duplicate, Rejection(result));
    }

    [Fact]
    public async Task Position_RequiresAnExistingDiscipline()
    {
        var result = await _bed.WithCommandsAsync<PositionDraft, VersionedSaveResult>(
            commands => commands.Create(new PositionDraft(Guid.NewGuid(), "Keeper", "GK", 1)));

        Assert.Equal(SportRejections.UnknownReference, Rejection(result));
    }

    [Fact]
    public async Task Season_RejectsAnEndBeforeTheStart()
    {
        var discipline = await _bed.CreateDisciplineAsync();

        var result = await _bed.WithCommandsAsync<SeasonDraft, VersionedSaveResult>(
            commands => commands.Create(new SeasonDraft(discipline, "S", new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1))));

        Assert.Equal(SportRejections.InvalidRange, Rejection(result));
    }

    [Fact]
    public async Task Team_RejectsAnUnknownLogoAndAttachesAKnownOneToTheTeam()
    {
        var discipline = await _bed.CreateDisciplineAsync();
        var unknown = await _bed.WithCommandsAsync<TeamDraft, VersionedSaveResult>(
            commands => commands.Create(new TeamDraft(discipline, "Rovers", null, null, null, null, null, Guid.NewGuid())));
        var logo = await _bed.Files.Add(new IModuleFiles.Upload(SportFileTypes.Teams, "l.png", "image/png", [1]));

        var team = await _bed.CreateTeamAsync(discipline, logo: logo);

        Assert.Equal(SportRejections.UnknownImage, Rejection(unknown));
        var attached = Assert.Single(_bed.Files.Attached);
        Assert.Equal((SportFileTypes.Teams, team), (attached.Type, attached.ParentId));
        Assert.Equal([logo], attached.Keys);
        Assert.Equal(SportFileTypes.TeamImage(logo), Assert.Single(await _bed.ListAsync<TeamListing>()).LogoUrl);
    }

    [Fact]
    public async Task Team_WithoutALogoHasNoLogoUrl()
    {
        var discipline = await _bed.CreateDisciplineAsync();
        await _bed.CreateTeamAsync(discipline);

        Assert.Null(Assert.Single(await _bed.ListAsync<TeamListing>()).LogoUrl);
    }

    [Fact]
    public async Task Player_RejectsAPositionOfAnotherSport()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var hockey = await _bed.CreateDisciplineAsync("Hockey", "hockey");
        var position = await _bed.CreatePositionAsync(hockey, "Goalie");

        var result = await _bed.WithCommandsAsync<PlayerDraft, VersionedSaveResult>(
            commands => commands.Create(new PlayerDraft(soccer, "A", "B", null, null, position, null, null, null, null)));

        Assert.Equal(SportRejections.Mismatch, Rejection(result));
    }

    [Fact]
    public async Task Player_RejectsAnUnknownPosition()
    {
        var soccer = await _bed.CreateDisciplineAsync();

        var result = await _bed.WithCommandsAsync<PlayerDraft, VersionedSaveResult>(
            commands => commands.Create(new PlayerDraft(soccer, "A", "B", null, null, Guid.NewGuid(), null, null, null, null)));

        Assert.Equal(SportRejections.UnknownReference, Rejection(result));
    }

    [Fact]
    public async Task Membership_RejectsDuplicatesMixedSportsAndUnknownPictures()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var hockey = await _bed.CreateDisciplineAsync("Hockey", "hockey");
        var player = await _bed.CreatePlayerAsync(soccer);
        var season = await _bed.CreateSeasonAsync(soccer);
        var team = await _bed.CreateTeamAsync(soccer);
        var hockeyTeam = await _bed.CreateTeamAsync(hockey, "Sticks");
        await _bed.CreateMembershipAsync(player, season, team);

        var duplicate = await _bed.WithCommandsAsync<PlayerSeasonDraft, VersionedSaveResult>(
            commands => commands.Create(new PlayerSeasonDraft(player, season, team, 1, null)));
        var next = await _bed.CreateSeasonAsync(soccer, "next");
        var later = await _bed.CreateSeasonAsync(soccer, "later");
        var mixed = await _bed.WithCommandsAsync<PlayerSeasonDraft, VersionedSaveResult>(
            commands => commands.Create(new PlayerSeasonDraft(player, next, hockeyTeam, 1, null)));
        var picture = await _bed.WithCommandsAsync<PlayerSeasonDraft, VersionedSaveResult>(
            commands => commands.Create(new PlayerSeasonDraft(player, later, team, 1, Guid.NewGuid())));
        var missing = await _bed.WithCommandsAsync<PlayerSeasonDraft, VersionedSaveResult>(
            commands => commands.Create(new PlayerSeasonDraft(player, Guid.NewGuid(), team, 1, null)));

        Assert.Equal(SportRejections.Duplicate, Rejection(duplicate));
        Assert.Equal(SportRejections.Mismatch, Rejection(mixed));
        Assert.Equal(SportRejections.UnknownImage, Rejection(picture));
        Assert.Equal(SportRejections.UnknownReference, Rejection(missing));
    }

    [Fact]
    public async Task Membership_AttachesThePictureToThePlayerAndKeepsOnePicturePerSeason()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var player = await _bed.CreatePlayerAsync(soccer);
        var team = await _bed.CreateTeamAsync(soccer);
        var first = await _bed.CreateSeasonAsync(soccer, "2024/25", new DateOnly(2024, 8, 1));
        var second = await _bed.CreateSeasonAsync(soccer, "2025/26");
        var oldPicture = await _bed.Files.Add(new IModuleFiles.Upload(SportFileTypes.Players, "o.png", "image/png", [1]));
        var newPicture = await _bed.Files.Add(new IModuleFiles.Upload(SportFileTypes.Players, "n.png", "image/png", [2]));

        await _bed.CreateMembershipAsync(player, first, team, 7, oldPicture);
        await _bed.CreateMembershipAsync(player, second, team, 9, newPicture);

        Assert.All(_bed.Files.Attached.Where(attached => attached.Keys.Count > 0), attached => Assert.Equal((SportFileTypes.Players, player), (attached.Type, attached.ParentId)));
        var memberships = await _bed.ListAsync<PlayerSeasonListing>();
        Assert.Equal(
            [SportFileTypes.PlayerImage(oldPicture), SportFileTypes.PlayerImage(newPicture)],
            memberships.OrderBy(row => row.ShirtNumber).Select(row => row.PictureUrl));
    }

    [Fact]
    public async Task Stat_RejectsDuplicatesMixedSportsAndUnknownSubjects()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var hockey = await _bed.CreateDisciplineAsync("Hockey", "hockey");
        var season = await _bed.CreateSeasonAsync(soccer);
        var goals = await _bed.CreateStatTypeAsync(soccer);
        var player = await _bed.CreatePlayerAsync(soccer);
        var hockeyPlayer = await _bed.CreatePlayerAsync(hockey, "H", "P");
        await _bed.CreateStatAsync(season, goals, player, 3);

        var duplicate = await _bed.WithCommandsAsync<StatDraft, VersionedSaveResult>(
            commands => commands.Create(new StatDraft(season, goals, player, 4)));
        var mixed = await _bed.WithCommandsAsync<StatDraft, VersionedSaveResult>(
            commands => commands.Create(new StatDraft(season, goals, hockeyPlayer, 1)));
        var unknown = await _bed.WithCommandsAsync<StatDraft, VersionedSaveResult>(
            commands => commands.Create(new StatDraft(season, goals, Guid.NewGuid(), 1)));
        var wins = await _bed.CreateStatTypeAsync(soccer, "Wins", StatScope.Team);
        var wrongScope = await _bed.WithCommandsAsync<StatDraft, VersionedSaveResult>(
            commands => commands.Create(new StatDraft(season, wins, player, 1)));

        Assert.Equal(SportRejections.Duplicate, Rejection(duplicate));
        Assert.Equal(SportRejections.Mismatch, Rejection(mixed));
        Assert.Equal(SportRejections.UnknownReference, Rejection(unknown));
        Assert.Equal(SportRejections.UnknownReference, Rejection(wrongScope));
    }

    [Fact]
    public async Task StatType_CannotChangeItsScopeWhileStatsUseIt()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var season = await _bed.CreateSeasonAsync(soccer);
        var goals = await _bed.CreateStatTypeAsync(soccer);
        await _bed.CreateStatAsync(season, goals, await _bed.CreatePlayerAsync(soccer), 1);

        var result = await _bed.WithCommandsAsync<StatTypeDraft, VersionedSaveResult>(
            commands => commands.Update(goals, new StatTypeDraft(soccer, "Goals", "G", StatScope.Team, true)));
        var renamed = await _bed.WithCommandsAsync<StatTypeDraft, VersionedSaveResult>(
            commands => commands.Update(goals, new StatTypeDraft(soccer, "Goals scored", "G", StatScope.Player, true)));

        Assert.Equal(SportRejections.Mismatch, Rejection(result));
        Assert.IsType<VersionedSaveResult.Saved>(renamed);
    }

    [Fact]
    public async Task Delete_SoftDeletesRestoreBringsItBackAndHardDeleteNeedsADeletedRow()
    {
        var discipline = await _bed.CreateDisciplineAsync();

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithCommandsAsync<DisciplineDraft, HardDeleteResult>(commands => commands.HardDelete(discipline)));
        Assert.True(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Delete(discipline)));
        Assert.Empty(await _bed.ListAsync<DisciplineListing>());
        Assert.True(Assert.Single(await _bed.ListAsync<DisciplineListing>(DeletionState.Deleted)).IsDeleted);
        Assert.True(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Restore(discipline)));
        Assert.Single(await _bed.ListAsync<DisciplineListing>());
        Assert.False(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Delete(Guid.NewGuid())));
        Assert.False(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedRowButRefusesWhileItIsReferenced()
    {
        var discipline = await _bed.CreateDisciplineAsync();
        var team = await _bed.CreateTeamAsync(discipline);
        var spare = await _bed.CreateTeamAsync(discipline, "Spare");
        await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Delete(discipline));
        await _bed.WithCommandsAsync<TeamDraft, bool>(commands => commands.Delete(spare));

        var referenced = await _bed.WithCommandsAsync<DisciplineDraft, HardDeleteResult>(commands => commands.HardDelete(discipline));
        var removed = await _bed.WithCommandsAsync<TeamDraft, HardDeleteResult>(commands => commands.HardDelete(spare));

        Assert.Equal(HardDeleteResult.Rejected, referenced);
        Assert.Equal(HardDeleteResult.Deleted, removed);
        Assert.Empty(await _bed.ListAsync<TeamListing>(DeletionState.Deleted));
        Assert.Equal([team], (await _bed.ListAsync<TeamListing>()).Select(row => row.Id));
    }

    [Fact]
    public async Task Ensure_CreatesOnlyOnceAndNeverRevivesADeletedRow()
    {
        var key = Guid.NewGuid();
        var draft = new DisciplineDraft("Soccer", "soccer");

        Assert.True(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Ensure(key, draft)));
        Assert.False(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Ensure(key, draft)));
        await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Delete(key));
        Assert.False(await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Ensure(key, draft)));
    }

    [Fact]
    public async Task Create_RecordsTheCreationInTheAuditLog()
    {
        var key = await _bed.CreateDisciplineAsync();
        await using var context = _bed.Read();

        var entries = context.AuditLogEntries.Where(entry => entry.EntityName == "Discipline" && entry.EntityKey == $"{key.ToString().ToLowerInvariant()}:1").ToList();

        Assert.Single(entries);
    }
}
