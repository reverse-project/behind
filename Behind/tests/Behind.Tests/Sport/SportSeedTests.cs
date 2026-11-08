using Behind.Sport.Database;
using Behind.Sport.Domain;

namespace Behind.Tests.Sport;

public sealed class SportSeedTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task SeedAsync()
    {
        await using var context = _bed.Write();

        await new SportSeed(
            new Behind.Sport.Database.Commands.DisciplineCommands(context),
            new Behind.Sport.Database.Commands.PositionCommands(context),
            new Behind.Sport.Database.Commands.StatTypeCommands(context)).SeedAsync();
    }

    [Fact]
    public async Task Seed_CreatesSoccerWithItsPositionsAndStatTypes()
    {
        await SeedAsync();

        Assert.Equal(["Soccer"], (await _bed.ListAsync<DisciplineListing>()).Select(row => row.Name));
        Assert.Equal(4, (await _bed.ListAsync<PositionListing>()).Count);
        var statTypes = await _bed.ListAsync<StatTypeListing>();
        Assert.Equal(SportSeed.SoccerStatTypes.Count, statTypes.Count);
        Assert.Contains(statTypes, row => row.Scope == StatScope.Team && !row.HigherIsBetter);
    }

    [Fact]
    public async Task Seed_IsIdempotentAndKeepsDeletedRowsDeleted()
    {
        await SeedAsync();
        await _bed.WithCommandsAsync<PositionDraft, bool>(commands => commands.Delete(SportSeed.SoccerPositions[0].Id));

        await SeedAsync();

        Assert.Equal(3, (await _bed.ListAsync<PositionListing>()).Count);
        Assert.Single(await _bed.ListAsync<DisciplineListing>());
    }
}
