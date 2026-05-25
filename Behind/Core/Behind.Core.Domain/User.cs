using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public abstract record User(Option<IEnumerable<Role>> Roles);

public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }

    public HumanUser() : this([], None)
    {
    }

    public HumanUser(IEnumerable<Email> emails, Option<IEnumerable<Role>> Roles) : base(Roles)
    {
        var emailList = emails.ToList();

        if (emailList.Count == 0)
        {
            throw new ArgumentException("A HumanUser must have at least one email address.", nameof(emails));
        }

        Emails = emailList;
    }
}

public sealed record SystemUser(Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record GuestUser(Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record BannedUser() : User(None);