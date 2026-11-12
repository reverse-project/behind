using Behind.Common.Modules;
using Behind.Forum.Common;
using Behind.Forum.Database.Aggregates;
using Behind.Forum.Domain;

namespace Behind.Forum.WebApi;

public static class ForumModule
{
    public static ModuleDescriptor Descriptor { get; } = new(ForumNames.Key, ForumNames.Name)
    {
        GrantAreas =
        [
            new ModuleGrantArea("Forum", ModuleAudience.Everyone),
            new ModuleGrantArea("ForumModeration", ModuleAudience.Administration)
        ],
        MenuItems =
        [
            new ModuleMenuItem(new Guid("a6f5bf88-13a3-4ee3-bc4c-f0f54d715d36"), "Forum", "#/forum", 90)
        ],
        AdministrationPages =
        [
            new ModuleAdministrationPage("forum.manage", "fa-comments", "#/manage/forum", ForumNames.Manage),
            new ModuleAdministrationPage("forum.moderation", "fa-shield", "#/manage/forum", ForumNames.Moderate)
        ],
        Routes =
        [
            new ModuleRoute("#/forum", "modules/forum/controllers/forum"),
            new ModuleRoute("#/manage/forum", "modules/forum/controllers/forummanager")
        ],
        Widgets =
        [
            new ModuleWidget("forum.posts", "widgets.forumPosts", "modules/forum/models/widgets"),
            new ModuleWidget("forum.stats", "widgets.forumStats", "modules/forum/models/widgets")
        ],
        ConfigurationFiles = ["forum.json"],
        Frontend = new ModuleFrontend(typeof(ForumModule).Assembly, ["modules/forum/css/forum.css"]),
        Database = new ModuleDatabase(typeof(ForumBoardConfiguration).Assembly)
        {
            ContentRestrictedEntities = [typeof(ForumBoardCategory), typeof(ForumBoard), typeof(ForumPost)],
            ContentLocaleEditPolicies = new Dictionary<string, string>
            {
                [nameof(ForumBoardCategory)] = ForumNames.Manage,
                [nameof(ForumBoard)] = ForumNames.Manage
            },
            SoftDeletedEntities = [typeof(ForumBoardCategory), typeof(ForumBoard), typeof(ForumPost), typeof(ForumUserModeration)]
        }
    };
}
