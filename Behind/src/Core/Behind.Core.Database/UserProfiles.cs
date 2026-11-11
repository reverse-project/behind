using Behind.Common.Database.Modules;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class UserProfiles(AbstractCoreContext context, IModuleFiles files) : IModuleUsers
{
    public const string FileType = "Users";

    public async Task<IReadOnlyList<IModuleUsers.Profile>> Find(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await context.Users.Where(user => ids.Contains(EF.Property<Guid>(user, "Id")))
            .Select(user => new IModuleUsers.Profile(
                EF.Property<Guid>(user, "Id"),
                user.DisplayName.Trim() != ""
                    ? user.DisplayName.Trim()
                    : context.Emails
                        .Where(email => EF.Property<Guid>(email, "UserId") == EF.Property<Guid>(user, "Id"))
                        .OrderBy(email => EF.Property<Guid>(email, "Id"))
                        .Select(email => email.Value)
                        .FirstOrDefault() ?? "",
                user.ProfilePictureId))
            .AsNoTracking().ToListAsync(cancellationToken);

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
