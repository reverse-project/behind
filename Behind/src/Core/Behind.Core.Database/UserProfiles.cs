using Behind.Common;
using Behind.Common.Database.Modules;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class UserProfiles(AbstractCoreContext context, IModuleFiles files) : IModuleUsers
{
    public const string FileType = "Users";

    public async Task<IReadOnlyList<IModuleUsers.Profile>> Find(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var users = await context.Users.Where(user => ids.Contains(EF.Property<Guid>(user, "Id")))
            .Select(user => new { Id = EF.Property<Guid>(user, "Id"), user.DisplayName, user.ProfilePictureId })
            .AsNoTracking().ToListAsync(cancellationToken);

        return users.Select(user => new IModuleUsers.Profile(
            user.Id, user.Id.ToUserLabel(user.DisplayName), user.ProfilePictureId)).ToList();
    }

    public sealed record Details(DateOnly? Birthday, bool HideEmail);

    public sealed record PublicProfile(Guid Id, string DisplayName, Guid? PictureId, string? PrimaryEmail);

    public async Task<Details?> GetDetails(Guid userId, CancellationToken cancellationToken) =>
        await context.Users.Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => new Details(user.Birthday, user.HideEmail))
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    public async Task<PublicProfile?> GetPublic(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => new
            {
                user.DisplayName,
                user.ProfilePictureId,
                PrimaryEmail = user.HideEmail ? null : context.Emails
                    .Where(email => EF.Property<Guid>(email, "UserId") == userId)
                    .OrderBy(email => EF.Property<Guid>(email, "Id"))
                    .Select(email => email.Value)
                    .FirstOrDefault()
            }).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : new PublicProfile(
            userId, userId.ToUserLabel(user.DisplayName), user.ProfilePictureId, user.PrimaryEmail);
    }

    public async Task<bool> SetDetails(Guid userId, Details details, CancellationToken cancellationToken)
    {
        var repository = context.GetRepository<HumanUser, Guid>();
        var found = await repository.TryFindAsync(userId, cancellationToken);

        return await found.Match(
            () => Task.FromResult(false),
            async user =>
            {
                repository.Update(userId, user with { Birthday = details.Birthday, HideEmail = details.HideEmail });
                await context.SaveChangesAsync(cancellationToken);
                return true;
            });
    }

    public async Task<bool> SetPicture(Guid userId, Guid? pictureId, CancellationToken cancellationToken)
    {
        var repository = context.GetRepository<HumanUser, Guid>();
        var found = await repository.TryFindAsync(userId, cancellationToken);

        return await found.Match(
            () => Task.FromResult(false),
            async user =>
            {
                if (pictureId is { } picture)
                {
                    await files.Attach(FileType, userId, [picture], cancellationToken);
                }

                repository.Update(userId, user with { ProfilePictureId = pictureId });
                await context.SaveChangesAsync(cancellationToken);

                return true;
            });
    }

    public Task<bool> IsCurrentPicture(Guid pictureId, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.ProfilePictureId == pictureId, cancellationToken);
}
