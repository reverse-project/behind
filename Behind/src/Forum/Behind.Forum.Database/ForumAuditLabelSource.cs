using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Microsoft.EntityFrameworkCore;

namespace Behind.Forum.Database;

public sealed class ForumAuditLabelSource(IForumContext context, IModuleUsers users) : IAuditLabelSource
{
    public async Task<IReadOnlyList<AuditLabel>> LabelsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        using var scope = context.SoftDelete.IncludeDeleted();
        var categories = await context.ForumBoardCategories.LatestOf(context.ForumBoardCategories).OfKeys(ids)
            .Select(category => new AuditLabel(EF.Property<Guid>(category, VersionedColumns.Key), category.Name))
            .ToListAsync(cancellationToken);
        var boards = await context.ForumBoards.LatestOf(context.ForumBoards).OfKeys(ids)
            .Select(board => new AuditLabel(EF.Property<Guid>(board, VersionedColumns.Key), board.Name))
            .ToListAsync(cancellationToken);
        var posts = await context.ForumPosts.LatestOf(context.ForumPosts).OfKeys(ids)
            .Select(post => new AuditLabel(EF.Property<Guid>(post, VersionedColumns.Key),
                post.Title == "" ? post.Body : post.Title))
            .ToListAsync(cancellationToken);
        var rules = await context.ForumUserModerations.LatestOf(context.ForumUserModerations).OfKeys(ids)
            .Select(rule => new { Key = EF.Property<Guid>(rule, VersionedColumns.Key), rule.UserId })
            .ToListAsync(cancellationToken);
        var profiles = await users.Find(rules.Select(rule => rule.UserId).ToArray(), cancellationToken);

        return categories.Concat(boards).Concat(posts)
            .Concat(rules.Select(rule => new AuditLabel(rule.Key,
                profiles.FirstOrDefault(profile => profile.Id == rule.UserId)?.DisplayName ?? "User")))
            .Select(label => label with { Label = label.Label.Shortened(80) })
            .ToList();
    }
}
