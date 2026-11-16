using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class PasswordCommands(AbstractCoreContext context) : IPasswordCommands
{
    public async Task InactivateForUser(User user, CancellationToken cancellationToken = default) =>
        await InactivateFromUserId(await context.GetUserKeyAsync(user, cancellationToken), cancellationToken);

    public async Task InactivateFromUserId(Guid id, CancellationToken cancellationToken = default)
    {
        var passwords = from password in context.Passwords
            where EF.Property<Guid>(password, "UserId") == id
            select new { Id = EF.Property<Guid>(password, "Id"), Password = password };

        var passwordList = await passwords.AsNoTracking().ToListAsync(cancellationToken);

        var repository = context.GetRepository<Password, Guid>();
        foreach (var userPassword in passwordList)
        {
            repository.Update(userPassword.Id, userPassword.Password with { IsActive = false });
        }
    }

    public async Task CreateForUser(User user, Password password, CancellationToken cancellationToken = default) =>
        await CreateFromUserId(await context.GetUserKeyAsync(user, cancellationToken), password, cancellationToken);

    public async Task CreateFromUserId(Guid id, Password password, CancellationToken cancellationToken = default)
    {
        await context.GetRepository<Password, Guid>().AddAsync(password, cancellationToken);
        context.Entry(password).Property<Guid>("UserId").CurrentValue = id;
    }
}