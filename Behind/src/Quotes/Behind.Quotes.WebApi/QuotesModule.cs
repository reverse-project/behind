using Behind.Common.Modules;
using Behind.Quotes.Common;
using Behind.Quotes.Database.Aggregates;
using Behind.Quotes.Domain;

namespace Behind.Quotes.WebApi;

public static class QuotesModule
{
    private const string Icon = "fa-quote-left";
    private const int MenuPosition = 70;
    private const string PublicController = "modules/quotes/controllers/quotes";
    private const string ManageController = "modules/quotes/controllers/quotemanager";

    public static ModuleDescriptor Descriptor { get; } = new(QuotesModuleNames.Key, QuotesModuleNames.Name)
    {
        GrantAreas = [new ModuleGrantArea(QuotesModuleNames.Area, ModuleAudience.Everyone)],
        MenuItems =
        [
            new ModuleMenuItem(
                QuotesModuleNames.MenuItemId,
                QuotesModuleNames.Name,
                QuotesModuleNames.PublicRoute,
                MenuPosition)
        ],
        AdministrationPages =
        [
            new ModuleAdministrationPage(
                QuotesModuleNames.AdministrationNameKey,
                Icon,
                QuotesModuleNames.ManageRoute,
                QuotesModuleNames.WriteGrant)
        ],
        Routes =
        [
            new ModuleRoute(QuotesModuleNames.PublicRoute, PublicController),
            new ModuleRoute(QuotesModuleNames.ManageRoute, ManageController)
        ],
        Frontend = new ModuleFrontend(typeof(QuotesModule).Assembly),
        Database = new ModuleDatabase(typeof(QuoteConfiguration).Assembly)
        {
            SoftDeletedEntities = [typeof(Quote)]
        }
    };
}
