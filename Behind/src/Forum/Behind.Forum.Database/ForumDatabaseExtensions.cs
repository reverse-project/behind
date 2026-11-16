using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Forum.Database;

public static class ForumDatabaseExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddForumDatabase() =>
            services.AddModuleContext<IForumContext>()
            .AddScoped<IVersionedCommands<ForumBoardCategory>, ForumBoardCategoryCommands>()
            .AddScoped<IVersionedCommands<ForumBoard>, ForumBoardCommands>()
                .AddScoped<IVersionedCommands<ForumPost>, ForumPostCommands>()
                .AddScoped<IVersionedCommands<ForumUserModeration>, ForumUserModerationCommands>()
                .AddScoped<ForumQueries>()
                .AddScoped<IAuditLabelSource, ForumAuditLabelSource>();
    }
}
