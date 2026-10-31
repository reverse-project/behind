using Behind.Common;
using Behind.Common.Database;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRepository<Session, Guid>
{
    public DbSet<ValidSession> Sessions => Set<ValidSession>();

    public Guid GetKey(Session entity) =>
        entity switch
        {
            ValidSession valid => Entry(valid).Property<Guid>("Id").CurrentValue,
            _ => throw new ArgumentException("Only a valid session carries its key.", nameof(entity))
        };

    async Task<Option<Session>> IRepository<Session, Guid>.TryFindAsync(
        Guid key,
        CancellationToken cancellationToken
    )
    {
        var row = await Sessions
            .Where(s => EF.Property<Guid>(s, "Id") == key)
            .Select(s => new
            {
                Session = s,
                UserId = EF.Property<Guid>(s, "UserId"),
                UserAgent = UserAgents
                    .Where(a => EF.Property<Guid>(a, "Id") == EF.Property<Guid>(s, "UserAgentId"))
                    .Select(a => a.Value)
                    .FirstOrDefault(),
                InvalidatedById = EF.Property<Guid?>(s, "InvalidatedById"),
                InvalidatedAt = EF.Property<DateTimeOffset?>(s, "InvalidatedAt")
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return None;
        }

        return row.InvalidatedById.HasValue || row.InvalidatedAt.HasValue
            ? Some(await ToInvalidSessionAsync(row.InvalidatedById, row.InvalidatedAt, cancellationToken))
            : Some(await ToValidSessionAsync(row.Session, row.UserId, row.UserAgent, cancellationToken));
    }

    async Task IRepository<Session, Guid>.AddAsync(Session aggregate, CancellationToken cancellationToken)
    {
        if (aggregate is not ValidSession valid)
        {
            throw new ArgumentException("Only a valid session can be added.", nameof(aggregate));
        }

        var userAgentKey = await GetOrAddUserAgentKeyAsync(valid.UserAgent, cancellationToken);
        await Sessions.AddAsync(valid, cancellationToken);
        Entry(valid).Property("UserAgentId").CurrentValue = userAgentKey;
    }

    void IRepository<Session, Guid>.Update(Session aggregate) =>
        ((IRepository<Session, Guid>)this).Update(GetKey(aggregate), aggregate);

    void IRepository<Session, Guid>.Update(Guid key, Session aggregate)
    {
        switch (aggregate)
        {
            case ValidSession valid:
                ReplaceSession(key, valid);
                break;
            case InvalidSession invalid:
                InvalidateSession(key, invalid);
                break;
        }
    }

    void IRepository<Session, Guid>.Delete(Session aggregate)
    {
        if (aggregate is not ValidSession valid)
        {
            throw new ArgumentException("Only a valid session can be deleted.", nameof(aggregate));
        }

        Sessions.ToImmutableEntityRepository(this).RemoveImmutable(valid);
    }

    private async Task<Session> ToValidSessionAsync(
        ValidSession session,
        Guid userId,
        string userAgent,
        CancellationToken cancellationToken
    )
    {
        var user = (await TryResolveUserAsync(userId, cancellationToken)).GetOrElse(new GuestUser(None));

        return session with
        {
            User = user,
            UserAgent = new UserAgent(userAgent),
            AccessToken = session.AccessToken with { User = user },
            RefreshToken = session.RefreshToken with { User = user }
        };
    }

    private async Task<Session> ToInvalidSessionAsync(
        Guid? invalidatedById,
        DateTimeOffset? invalidatedAt,
        CancellationToken cancellationToken
    )
    {
        Option<User> invalidatedBy = invalidatedById.HasValue
            ? await TryResolveUserAsync(invalidatedById.Value, cancellationToken)
            : None;

        Option<DateTimeOffset> at = invalidatedAt.HasValue ? Some(invalidatedAt.Value) : None;

        return new InvalidSession(invalidatedBy, at);
    }

    private async Task<Option<User>> TryResolveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == DomainConstants.SystemUserId)
        {
            return Some<User>(new SystemUser(None));
        }

        if (userId == DomainConstants.GuestUserId)
        {
            return Some<User>(new GuestUser(None));
        }

        var human = await ((IRepository<HumanUser, Guid>)this).TryFindAsync(userId, cancellationToken);
        return human.Map(user => (User)user);
    }

    private void ReplaceSession(Guid key, ValidSession replacement)
    {
        var original = FindTrackedSession(key);
        var userAgentKey = GetOrAddUserAgentKey(replacement.UserAgent);
        var userId = original.Property<Guid>("UserId").CurrentValue;
        var invalidatedById = original.Property<Guid?>("InvalidatedById").CurrentValue;
        var invalidatedAt = original.Property<DateTimeOffset?>("InvalidatedAt").CurrentValue;

        Sessions.ToImmutableEntityRepository(this).UpdateImmutable([key], replacement);

        var updated = Entry(replacement);
        updated.Property("UserId").CurrentValue = userId;
        updated.Property("UserAgentId").CurrentValue = userAgentKey;
        updated.Property("InvalidatedById").CurrentValue = invalidatedById;
        updated.Property("InvalidatedAt").CurrentValue = invalidatedAt;
    }

    private void InvalidateSession(Guid key, InvalidSession invalid)
    {
        if (invalid.InvalidatedBy == None && invalid.InvalidatedAt == None)
        {
            throw new ArgumentException(
                "An invalid session must have InvalidatedBy or InvalidatedAt populated.",
                nameof(invalid));
        }

        var entry = FindTrackedSession(key);
        entry.Property("InvalidatedById").CurrentValue =
            invalid.InvalidatedBy.Match(() => (Guid?)null, user => ResolveUserKey(user));
        entry.Property("InvalidatedAt").CurrentValue =
            invalid.InvalidatedAt.Match(() => (DateTimeOffset?)null, at => at);
    }

    private EntityEntry<ValidSession> FindTrackedSession(Guid key) =>
        ChangeTracker.Entries<ValidSession>()
            .FirstOrDefault(entry => entry.Property<Guid>("Id").CurrentValue == key)
        ?? throw new InvalidOperationException("Original entity must be tracked.");

    private Guid ResolveUserKey(User user) =>
        user switch
        {
            SystemUser => DomainConstants.SystemUserId,
            GuestUser => DomainConstants.GuestUserId,
            HumanUser human => FindHumanUserKey(human),
            _ => throw new ArgumentException("The user cannot invalidate a session.", nameof(user))
        };

    private Guid FindHumanUserKey(HumanUser human)
    {
        var values = human.Emails.Select(email => email.Value).ToList();

        return Emails
                   .Where(email => values.Contains(email.Value))
                   .Select(email => (Guid?)EF.Property<Guid>(email, "UserId"))
                   .FirstOrDefault()
               ?? throw new InvalidOperationException("The user does not exist.");
    }
}
