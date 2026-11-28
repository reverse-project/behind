using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public abstract record User(Option<IEnumerable<Role>> Roles);

public sealed record HumanUser : User
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? PersistedId { get; init; }

    public IEnumerable<Email> Emails { get; init; }

    public string DisplayName { get; init; }

    public Guid? ProfilePictureId { get; init; }

    [Sensible]
    public DateOnly? Birthday { get; init; }

    [Sensible]
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? PendingParentId { get; init; }

    [Sensible]
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? PendingLinkRequestId { get; init; }

    public bool HideEmail { get; init; } = true;

    public HumanUser() : base(None)
    {
        Emails = new List<Email>();
        DisplayName = string.Empty;
    }

    public HumanUser(string displayName, IEnumerable<Email> emails, Option<IEnumerable<Role>> Roles) : base(Roles)
    {
        DisplayName = displayName;
        var emailList = emails.ToList();

        if (emailList.Count == 0)
        {
            throw new ArgumentException("A HumanUser must have at least one email address.", nameof(emails));
        }

        Emails = emailList;
    }
}

public sealed record ServiceUser : User
{
    public const string EntityType = nameof(User);
    public const int MaxNameLength = DisplayNameExtensions.MaxLength;

    public string Name { get; init; } = string.Empty;

    [Sensible]
    [System.Text.Json.Serialization.JsonIgnore]
    public string ApiKeyHash { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public ServiceUser() : base(None)
    {
    }

    public ServiceUser(string name, string apiKeyHash, bool enabled) : base(None)
    {
        Name = name;
        ApiKeyHash = apiKeyHash;
        Enabled = enabled;
    }

    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaxNameLength;
}

public sealed record ServiceUserVersion
{
    public VersionedKey<Guid> Id { get; init; } = default;
    public string Name { get; init; } = string.Empty;

    [Sensible]
    public string ApiKeyHash { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    private ServiceUserVersion()
    {
    }

    public ServiceUserVersion(VersionedKey<Guid> id, string name, string apiKeyHash, bool enabled) =>
        (Id, Name, ApiKeyHash, Enabled) = (id, name, apiKeyHash, enabled);
}

public sealed record SystemUser(Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record GuestUser(Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record BannedUser() : User(None);

public sealed record UnknownUser(Guid Id) : User(None);
