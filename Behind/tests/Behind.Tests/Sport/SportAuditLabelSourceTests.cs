using Behind.Common.Database.Modules;
using Behind.Sport.Database;
using Behind.Sport.Domain;

namespace Behind.Tests.Sport;

public sealed class SportAuditLabelSourceTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<AuditLabel>> LabelsAsync(params Guid[] ids)
    {
        await using var context = _bed.Read();

        return await new SportAuditLabelSource(context).LabelsAsync(ids);
    }

    [Fact]
    public async Task Labels_NameEveryEntityOfTheModule()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var position = await _bed.CreatePositionAsync(soccer);
        var statType = await _bed.CreateStatTypeAsync(soccer);
        var season = await _bed.CreateSeasonAsync(soccer);
        var team = await _bed.CreateTeamAsync(soccer);
        var player = await _bed.CreatePlayerAsync(soccer);
        var membership = await _bed.CreateMembershipAsync(player, season, team);
        var stat = await _bed.CreateStatAsync(season, statType, player, 3);

        var labels = (await LabelsAsync(soccer, position, statType, season, team, player, membership, stat))
            .ToDictionary(label => label.Id, label => label.Label);

        Assert.Equal("Soccer", labels[soccer]);
        Assert.Equal("Goalkeeper", labels[position]);
        Assert.Equal("Goals", labels[statType]);
        Assert.Equal("2025/26", labels[season]);
        Assert.Equal("Rovers", labels[team]);
        Assert.Equal("Ada Lovelace", labels[player]);
        Assert.Equal("Ada Lovelace - 2025/26 - Rovers", labels[membership]);
        Assert.Equal("Ada Lovelace - Goals - 2025/26", labels[stat]);
    }

    [Fact]
    public async Task Labels_StillNameDeletedRowsAndUseTheLatestVersion()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        await _bed.WithCommandsAsync<DisciplineDraft, VersionedSaveResult>(commands => commands.Update(soccer, new DisciplineDraft("Football", "soccer")));
        await _bed.WithCommandsAsync<DisciplineDraft, bool>(commands => commands.Delete(soccer));

        Assert.Equal([new AuditLabel(soccer, "Football")], await LabelsAsync(soccer));
    }

    [Fact]
    public async Task Labels_IgnoreUnknownIds()
    {
        Assert.Empty(await LabelsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Labels_NameTeamStats()
    {
        var soccer = await _bed.CreateDisciplineAsync();
        var season = await _bed.CreateSeasonAsync(soccer);
        var wins = await _bed.CreateStatTypeAsync(soccer, "Wins", StatScope.Team);
        var team = await _bed.CreateTeamAsync(soccer);
        var stat = await _bed.CreateStatAsync(season, wins, team, 4);

        Assert.Equal("Rovers - Wins - 2025/26", Assert.Single(await LabelsAsync(stat)).Label);
    }
}
