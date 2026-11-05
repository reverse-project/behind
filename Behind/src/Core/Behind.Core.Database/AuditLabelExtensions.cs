using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class AuditLabelExtensions
{
    private const string SessionKind = "Session";
    private const string PasswordKind = "Password";
    private const string MagicLinkKind = "Magic link";
    private const string WallOfShameKind = "Wall of shame entry";
    private const string NewsCommentKind = "News comment";
    private const string GuestLabel = "Guest";
    private const string GalleryItemKind = "Image";
    private const int GalleryItemLabelLength = 80;

    private sealed record Named(Guid Id, string Label);

    private sealed record Owned(Guid Id, string Kind, Guid UserId);

    private static IEnumerable<Named> BuiltInUsers =>
    [
        new(DomainConstants.SystemUserId, DomainConstants.System),
        new(DomainConstants.GuestUserId, GuestLabel)
    ];

    extension(string description)
    {
        private string Labelled(int maxLength) =>
            string.IsNullOrWhiteSpace(description) ? GalleryItemKind : description.Shortened(maxLength);
    }

    extension(AbstractCoreContext context)
    {
        public async Task<AuditLabels> ResolveAuditLabelsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default
        )
        {
            var wanted = ids.Distinct().ToList();
            var owned = await context.OwnedAsync(wanted, cancellationToken);
            var userIds = wanted.Concat(owned.Select(item => item.UserId)).Distinct().ToList();
            var users = (await context.UsersAsync(userIds, cancellationToken))
                .Concat(BuiltInUsers)
                .DistinctBy(item => item.Id)
                .ToDictionary(item => item.Id, item => item.Label);
            var named = await context.NamedAsync(wanted, cancellationToken);

            var ownedLabels = owned.Select(item => new Named(
                item.Id,
                $"{item.Kind} of {users.GetValueOrDefault(item.UserId, item.UserId.ToString())}"));

            return new AuditLabels(
                users.Select(pair => new Named(pair.Key, pair.Value))
                    .Concat(named)
                    .Concat(ownedLabels)
                    .DistinctBy(item => item.Id)
                    .ToDictionary(item => item.Id, item => item.Label));
        }

        private async Task<IReadOnlyList<Named>> UsersAsync(List<Guid> ids, CancellationToken cancellationToken) =>
            await context.Users
                .Where(user => ids.Contains(EF.Property<Guid>(user, "Id")))
                .Select(user => new Named(EF.Property<Guid>(user, "Id"), user.DisplayName))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<Named>> NamedAsync(List<Guid> ids, CancellationToken cancellationToken) =>
        [
            .. await context.Roles
                .Where(role => ids.Contains(EF.Property<Guid>(role, "Id")))
                .Select(role => new Named(EF.Property<Guid>(role, "Id"), role.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Grants
                .Where(grant => ids.Contains(EF.Property<Guid>(grant, "Id")))
                .Select(grant => new Named(EF.Property<Guid>(grant, "Id"), grant.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Restrictions
                .Where(restriction => ids.Contains(EF.Property<Guid>(restriction, "Id")))
                .Select(restriction => new Named(EF.Property<Guid>(restriction, "Id"), restriction.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Emails
                .Where(email => ids.Contains(EF.Property<Guid>(email, "Id")))
                .Select(email => new Named(EF.Property<Guid>(email, "Id"), email.Value))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.UserAgents
                .Where(agent => ids.Contains(EF.Property<Guid>(agent, "Id")))
                .Select(agent => new Named(EF.Property<Guid>(agent, "Id"), agent.Value))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Sites
                .Where(site => ids.Contains(EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty)))
                .OrderByDescending(site => EF.Property<long>(site, SiteConfiguration.VersionProperty))
                .Select(site => new Named(EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty), site.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.NewsEntries
                .Where(news => ids.Contains(EF.Property<Guid>(news, VersionedColumns.Key)))
                .OrderByDescending(news => EF.Property<long>(news, VersionedColumns.Version))
                .Select(news => new Named(EF.Property<Guid>(news, VersionedColumns.Key), news.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.NewsCategories
                .Where(category => ids.Contains(EF.Property<Guid>(category, VersionedColumns.Key)))
                .OrderByDescending(category => EF.Property<long>(category, VersionedColumns.Version))
                .Select(category => new Named(
                    EF.Property<Guid>(category, VersionedColumns.Key),
                    category.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Articles
                .Where(article => ids.Contains(EF.Property<Guid>(article, VersionedColumns.Key)))
                .OrderByDescending(article => EF.Property<long>(article, VersionedColumns.Version))
                .Select(article => new Named(EF.Property<Guid>(article, VersionedColumns.Key), article.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.ArticleCategories
                .Where(category => ids.Contains(EF.Property<Guid>(category, VersionedColumns.Key)))
                .OrderByDescending(category => EF.Property<long>(category, VersionedColumns.Version))
                .Select(category => new Named(
                    EF.Property<Guid>(category, VersionedColumns.Key),
                    category.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Galleries
                .Where(gallery => ids.Contains(EF.Property<Guid>(gallery, VersionedColumns.Key)))
                .OrderByDescending(gallery => EF.Property<long>(gallery, VersionedColumns.Version))
                .Select(gallery => new Named(EF.Property<Guid>(gallery, VersionedColumns.Key), gallery.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. (await context.GalleryItems
                    .Where(item => ids.Contains(EF.Property<Guid>(item, VersionedColumns.Key)))
                    .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
                    .Select(item => new Named(EF.Property<Guid>(item, VersionedColumns.Key), item.Description))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken))
                .Select(item => item with { Label = item.Label.Labelled(GalleryItemLabelLength) }),
            .. (await context.CarouselSlides
                    .Where(slide => ids.Contains(EF.Property<Guid>(slide, VersionedColumns.Key)))
                    .OrderByDescending(slide => EF.Property<long>(slide, VersionedColumns.Version))
                    .Select(slide => new Named(EF.Property<Guid>(slide, VersionedColumns.Key), slide.Caption))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken))
                .Select(slide => slide with { Label = slide.Label.Labelled(GalleryItemLabelLength) }),
            .. await context.DownloadFolders
                .Where(folder => ids.Contains(EF.Property<Guid>(folder, VersionedColumns.Key)))
                .OrderByDescending(folder => EF.Property<long>(folder, VersionedColumns.Version))
                .Select(folder => new Named(EF.Property<Guid>(folder, VersionedColumns.Key), folder.DisplayName))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Downloads
                .Where(download => ids.Contains(EF.Property<Guid>(download, VersionedColumns.Key)))
                .OrderByDescending(download => EF.Property<long>(download, VersionedColumns.Version))
                .Select(download => new Named(EF.Property<Guid>(download, VersionedColumns.Key), download.DisplayName))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.VfsFiles
                .Where(file => ids.Contains(EF.Property<Guid>(file, VersionedColumns.Key)))
                .Select(file => new Named(EF.Property<Guid>(file, VersionedColumns.Key), file.FileName))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Menus
                .Where(menu => ids.Contains(EF.Property<Guid>(menu, VersionedColumns.Key)))
                .OrderByDescending(menu => EF.Property<long>(menu, VersionedColumns.Version))
                .Select(menu => new Named(EF.Property<Guid>(menu, VersionedColumns.Key), menu.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.MenuItems
                .Where(item => ids.Contains(EF.Property<Guid>(item, VersionedColumns.Key)))
                .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
                .Select(item => new Named(EF.Property<Guid>(item, VersionedColumns.Key), item.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken)
        ];

        private async Task<IReadOnlyList<Owned>> OwnedAsync(List<Guid> ids, CancellationToken cancellationToken) =>
        [
            .. await context.Sessions
                .Where(session => ids.Contains(EF.Property<Guid>(session, "Id")))
                .Select(session => new Owned(
                    EF.Property<Guid>(session, "Id"),
                    SessionKind,
                    EF.Property<Guid>(session, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Passwords
                .Where(password => ids.Contains(EF.Property<Guid>(password, "Id")))
                .Select(password => new Owned(
                    EF.Property<Guid>(password, "Id"),
                    PasswordKind,
                    EF.Property<Guid>(password, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.MagicLinks
                .Where(link => ids.Contains(EF.Property<Guid>(link, "Id")))
                .Select(link => new Owned(
                    EF.Property<Guid>(link, "Id"),
                    MagicLinkKind,
                    EF.Property<Guid>(link, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.WallOfShameEntries
                .Where(entry => ids.Contains(EF.Property<Guid>(entry, "Id")))
                .Select(entry => new Owned(
                    EF.Property<Guid>(entry, "Id"),
                    WallOfShameKind,
                    EF.Property<Guid>(entry, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.NewsComments
                .Where(comment => ids.Contains(EF.Property<Guid>(comment, VersionedColumns.Key)))
                .OrderByDescending(comment => EF.Property<long>(comment, VersionedColumns.Version))
                .Select(comment => new Owned(
                    EF.Property<Guid>(comment, VersionedColumns.Key),
                    NewsCommentKind,
                    EF.Property<Guid>(comment, NewsCommentConfiguration.AuthorIdProperty)))
                .AsNoTracking()
                .ToListAsync(cancellationToken)
        ];
    }
}
