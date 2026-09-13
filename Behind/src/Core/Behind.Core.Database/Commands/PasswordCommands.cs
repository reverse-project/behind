using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class PasswordCommands(AbstractCoreContext context) : IPasswordCommands
{
    public Task InactivateForUser(User user, CancellationToken cancellationToken = default) =>
        InactivateFromUserId(context.GetKey((HumanUser)user), cancellationToken);

    public async Task InactivateFromUserId(Guid id, CancellationToken cancellationToken = default)
    {
        var passwords = from password in context.Passwords
            where EF.Property<Guid>(password, "UserId") == id
            select password;

        var passwordList = await passwords.AsNoTracking().ToListAsync(cancellationToken);

        var repository = context.GetRepository<Password, Guid>();
        foreach (var userPassword in passwordList)
        {
            var key = repository.GetKey(userPassword);
            var updated = userPassword with { IsActive = false };
            repository.Update(key, updated);
        }
    }

    public Task CreateForUser(User user, Password password, CancellationToken cancellationToken = default) =>
        CreateFromUserId(context.GetKey((HumanUser)user), password, cancellationToken);

    public Task CreateFromUserId(Guid id, Password password, CancellationToken cancellationToken = default)
    {
        var repository = context.GetRepository<Password, Guid>();
        
        repository.AddAsync(password, cancellationToken);
        context.Entry(password).Property<Guid>("UserId").CurrentValue = id;
        return Task.CompletedTask;
    }
}