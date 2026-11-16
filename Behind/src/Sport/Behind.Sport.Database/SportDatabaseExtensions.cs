using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Sport.Database.Commands;
using Behind.Sport.Database.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Sport.Database;

public static class SportDatabaseExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSportDatabase()
        {
            services.TryAddSingleton(TimeProvider.System);

            return services
                .AddModuleContext<ISportContext>()
                .AddScoped<IVersionedCommands<DisciplineDraft>, DisciplineCommands>()
                .AddScoped<IVersionedCommands<PositionDraft>, PositionCommands>()
                .AddScoped<IVersionedCommands<StatTypeDraft>, StatTypeCommands>()
                .AddScoped<IVersionedCommands<SeasonDraft>, SeasonCommands>()
                .AddScoped<IVersionedCommands<TeamDraft>, TeamCommands>()
                .AddScoped<IVersionedCommands<PlayerDraft>, PlayerCommands>()
                .AddScoped<IVersionedCommands<PlayerSeasonDraft>, PlayerSeasonCommands>()
                .AddScoped<IVersionedCommands<StatDraft>, StatCommands>()
                .AddScoped<IListingQueries<DisciplineListing>, DisciplineQueries>()
                .AddScoped<IListingQueries<PositionListing>, PositionQueries>()
                .AddScoped<IListingQueries<StatTypeListing>, StatTypeQueries>()
                .AddScoped<IListingQueries<SeasonListing>, SeasonQueries>()
                .AddScoped<IListingQueries<TeamListing>, TeamQueries>()
                .AddScoped<IListingQueries<PlayerListing>, PlayerQueries>()
                .AddScoped<IListingQueries<PlayerSeasonListing>, PlayerSeasonQueries>()
                .AddScoped<IListingQueries<StatListing>, StatQueries>()
                .AddScoped<ISportDetailQueries, SportDetailQueries>()
                .AddScoped<IAuditLabelSource, SportAuditLabelSource>()
                .AddScoped<IModuleSeed, SportSeed>();
        }
    }
}
