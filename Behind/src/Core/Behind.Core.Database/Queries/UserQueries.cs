using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class UserQueries(AbstractCoreContext context) : IUserQueries
{
    public async Task<Option<IUserQueries.UserResult>> GetUserByEmail(string email,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = from dbEmail in context.Emails
            where dbEmail.Value == email.ToLowerInvariant()
            join _user in context.Users on EF.Property<Guid>(dbEmail, "UserId") equals EF.Property<Guid>(_user, "Id")
                into dbUser

            from emailUser in dbUser.DefaultIfEmpty()
            select new {
                Id = EF.Property<Guid>(emailUser, "Id"),
                User = emailUser
            };

        var user = await searchQuery.AsNoTracking().FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (user is null)
        {
            return None;
        }

        var emails = from dbEmails in context.Emails
            where EF.Property<Guid>(dbEmails, "UserId") == user.Id
            select dbEmails;

        return new IUserQueries.UserResult(user.Id, user.User with { Emails = await emails.ToListAsync(cancellationToken) });
    }

    public async Task<bool> HasGrant(string email, string grant, CancellationToken cancellationToken = default)
    {
        var grantQuery = from dbEmail in context.Emails
            where dbEmail.Value == email.ToLowerInvariant()
            join userRole in context.Set<Dictionary<string, object>>("UserRoles")
                on EF.Property<Guid>(dbEmail, "UserId") equals EF.Property<Guid>(userRole, "UserId")
            join role in context.Roles
                on EF.Property<Guid>(userRole, "RoleId") equals EF.Property<Guid>(role, "Id")
            join roleGrant in context.Set<Dictionary<string, object>>("RoleGrants")
                on EF.Property<Guid>(role, "Id") equals EF.Property<Guid>(roleGrant, "RoleId")
            join dbGrant in context.Grants
                on EF.Property<Guid>(roleGrant, "GrantId") equals EF.Property<Guid>(dbGrant, "Id")
            where dbGrant.Name == grant
            select dbGrant;

        return await grantQuery.AnyAsync(cancellationToken);
    }
}
