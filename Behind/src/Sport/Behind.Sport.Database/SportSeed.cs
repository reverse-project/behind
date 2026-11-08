using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed class SportSeed(
    IVersionedCommands<DisciplineDraft> disciplines,
    IVersionedCommands<PositionDraft> positions,
    IVersionedCommands<StatTypeDraft> statTypes) : IModuleSeed
{
    public static readonly Guid SoccerId = SeedId(1);

    public static readonly DisciplineDraft Soccer = new("Soccer", "soccer");

    public static readonly IReadOnlyList<(Guid Id, PositionDraft Draft)> SoccerPositions =
    [
        (SeedId(10), new PositionDraft(SoccerId, "Goalkeeper", "GK", 1)),
        (SeedId(11), new PositionDraft(SoccerId, "Defender", "DF", 2)),
        (SeedId(12), new PositionDraft(SoccerId, "Midfielder", "MF", 3)),
        (SeedId(13), new PositionDraft(SoccerId, "Forward", "FW", 4))
    ];

    public static readonly IReadOnlyList<(Guid Id, StatTypeDraft Draft)> SoccerStatTypes =
    [
        (SeedId(20), new StatTypeDraft(SoccerId, "Appearances", "APP", StatScope.Player, true)),
        (SeedId(21), new StatTypeDraft(SoccerId, "Goals", "G", StatScope.Player, true)),
        (SeedId(22), new StatTypeDraft(SoccerId, "Assists", "A", StatScope.Player, true)),
        (SeedId(23), new StatTypeDraft(SoccerId, "Yellow cards", "YC", StatScope.Player, true)),
        (SeedId(24), new StatTypeDraft(SoccerId, "Red cards", "RC", StatScope.Player, true)),
        (SeedId(25), new StatTypeDraft(SoccerId, "Clean sheets", "CS", StatScope.Player, true)),
        (SeedId(30), new StatTypeDraft(SoccerId, "Played", "P", StatScope.Team, true)),
        (SeedId(31), new StatTypeDraft(SoccerId, "Won", "W", StatScope.Team, true)),
        (SeedId(32), new StatTypeDraft(SoccerId, "Drawn", "D", StatScope.Team, true)),
        (SeedId(33), new StatTypeDraft(SoccerId, "Lost", "L", StatScope.Team, false)),
        (SeedId(34), new StatTypeDraft(SoccerId, "Goals for", "GF", StatScope.Team, true)),
        (SeedId(35), new StatTypeDraft(SoccerId, "Goals against", "GA", StatScope.Team, false)),
        (SeedId(36), new StatTypeDraft(SoccerId, "Points", "PTS", StatScope.Team, true))
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await disciplines.Ensure(SoccerId, Soccer, cancellationToken);

        foreach (var (id, draft) in SoccerPositions)
        {
            await positions.Ensure(id, draft, cancellationToken);
        }

        foreach (var (id, draft) in SoccerStatTypes)
        {
            await statTypes.Ensure(id, draft, cancellationToken);
        }
    }

    private static Guid SeedId(int number) => Guid.Parse($"a4000000-0000-0000-0000-{number:D12}");
}
