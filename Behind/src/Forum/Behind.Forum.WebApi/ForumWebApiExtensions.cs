using Behind.Common.Configuration;
using Behind.Common.Modules;
using Behind.Common.WebApi;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Forum.WebApi;

public static class ForumWebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddForum()
        {
            builder.AddReloadableJsonFile("forum.json", true);
            builder.Services.AddOptions<ForumSettings>()
                .Bind(builder.Configuration.GetSection(ForumSettings.SectionName))
                .Validate(settings => Enum.IsDefined(settings.Moderation), "Invalid forum moderation mode.")
                .ValidateOnStart();
            builder.Services.AddModule(ForumModule.Descriptor).AddForumDatabase();
            builder.Services.AddScoped<IContentLocalePermission, ForumPostLocalePermission>();

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseForum()
        {
            application.MapForumEndpoints();

            return application;
        }
    }
}
