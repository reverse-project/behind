using Behind.Common.Modules;
using Behind.Sport.Common;
using Behind.Sport.Database.Aggregates;
using Behind.Sport.Domain;

namespace Behind.Sport.WebApi;

public static class SportModule
{
    private const string Icon = "fa-futbol";
    private const int MenuPosition = 75;
    private const string ControllerPrefix = "modules/sport/controllers/";

    private static readonly IReadOnlyList<ModuleRoute> Routes =
    [
        new("#/sport", ControllerPrefix + "sport"),
        new("#/sport/disciplines/:id", ControllerPrefix + "discipline"),
        new("#/sport/teams", ControllerPrefix + "teams"),
        new("#/sport/teams/discipline/:discipline", ControllerPrefix + "teams"),
        new("#/sport/teams/:id", ControllerPrefix + "team"),
        new("#/sport/players", ControllerPrefix + "players"),
        new("#/sport/players/discipline/:discipline", ControllerPrefix + "players"),
        new("#/sport/players/position/:position", ControllerPrefix + "players"),
        new("#/sport/players/:id", ControllerPrefix + "player"),
        new("#/sport/seasons", ControllerPrefix + "seasons"),
        new("#/sport/seasons/:id", ControllerPrefix + "season"),
        new("#/sport/leaderboards", ControllerPrefix + "leaderboards"),
        new("#/sport/leaderboards/:season", ControllerPrefix + "leaderboards"),
        new("#/sport/leaderboards/:season/:statType", ControllerPrefix + "leaderboards"),
        new("#/sport/stats", ControllerPrefix + "stats"),
        new("#/sport/stats/:season", ControllerPrefix + "stats"),
        new("#/sport/stats/:season/:statType", ControllerPrefix + "stats"),
        new(SportModuleNames.ManageRoute, ControllerPrefix + "manager"),
        new(SportModuleNames.ManageRoute + "/:resource", ControllerPrefix + "manager")
    ];

    public static ModuleDescriptor Descriptor { get; } = new(SportModuleNames.Key, SportModuleNames.Name)
    {
        GrantAreas = [new ModuleGrantArea(SportModuleNames.Area, ModuleAudience.Everyone)],
        MenuItems =
        [
            new ModuleMenuItem(
                SportModuleNames.MenuItemId,
                SportModuleNames.Name,
                SportModuleNames.PublicRoute,
                MenuPosition)
        ],
        AdministrationPages =
        [
            new ModuleAdministrationPage(
                SportModuleNames.AdministrationNameKey,
                Icon,
                SportModuleNames.ManageRoute,
                SportModuleNames.WriteGrant)
        ],
        Routes = Routes,
        Frontend = new ModuleFrontend(typeof(SportModule).Assembly),
        Database = new ModuleDatabase(typeof(DisciplineConfiguration).Assembly)
        {
            ContentLocaleEditPolicies = new[]
            {
                typeof(Discipline), typeof(Position), typeof(StatType), typeof(Season),
                typeof(Team), typeof(Player), typeof(PlayerSeason), typeof(Stat)
            }.ToDictionary(type => type.Name, _ => SportModuleNames.WriteGrant),
            ContentRestrictedEntities =
            [
                typeof(Discipline), typeof(Position), typeof(StatType), typeof(Season),
                typeof(Team), typeof(Player), typeof(PlayerSeason), typeof(Stat)
            ],
            SoftDeletedEntities =
            [
                typeof(Discipline),
                typeof(Position),
                typeof(StatType),
                typeof(Season),
                typeof(Team),
                typeof(Player),
                typeof(PlayerSeason),
                typeof(Stat)
            ]
        }
    };
}
