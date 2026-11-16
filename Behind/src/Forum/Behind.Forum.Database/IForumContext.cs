using Behind.Common.Database.Modules;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Forum.Database;

public interface IForumContext : IModuleContext
{
    DbSet<ForumBoardCategory> ForumBoardCategories { get; }

    DbSet<ForumBoard> ForumBoards { get; }

    DbSet<ForumPost> ForumPosts { get; }

    DbSet<ForumUserModeration> ForumUserModerations { get; }
}
