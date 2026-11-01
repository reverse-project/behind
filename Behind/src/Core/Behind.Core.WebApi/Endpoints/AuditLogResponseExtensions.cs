using System.Globalization;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class AuditLogResponseExtensions
{
    private const string OwnerProperty = "Owner";
    private const string ParentProperty = "Parent";

    extension(User user)
    {
        public AuditLogActor ToActor() =>
            user switch
            {
                HumanUser human => new AuditLogActor(
                    nameof(HumanUser),
                    human.DisplayName,
                    human.Emails.Select(email => email.Value).ToList()),
                SystemUser => new AuditLogActor(nameof(SystemUser), "System", []),
                GuestUser => new AuditLogActor(nameof(GuestUser), "Guest", []),
                UnknownUser unknown => new AuditLogActor(nameof(UnknownUser), $"Unknown user {unknown.Id}", []),
                _ => new AuditLogActor(user.GetType().Name, user.GetType().Name, [])
            };
    }

    extension(AuditLogEntry.AuditAction action)
    {
        public IReadOnlyList<AuditLogChange> ToChanges() =>
            action switch
            {
                AuditLogEntry.Updated updated => updated.Differences
                    .Select(difference => new AuditLogChange(
                        difference.PropertyName,
                        difference.OldValue.ToDisplayValue(),
                        difference.NewValue.ToDisplayValue()))
                    .ToList(),
                AuditLogEntry.ChangeOwnership ownership =>
                [
                    new AuditLogChange(OwnerProperty, ownership.OldOwnerId.ToString(), ownership.NewOwnerId.ToString())
                ],
                AuditLogEntry.Moved moved =>
                [
                    new AuditLogChange(ParentProperty, moved.OldParentId.ToString(), moved.NewParentId.ToString())
                ],
                _ => []
            };
    }

    extension(object? value)
    {
        private string? ToDisplayValue() => value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    extension(AuditLogEntry entry)
    {
        public AuditLogItem ToItem() =>
            new(
                entry.Timestamp,
                entry.EntityName,
                entry.EntityKey,
                entry.Action.GetType().Name,
                entry.Action.ToChanges(),
                entry.User.ToActor(),
                entry.BehalfOf.Match<AuditLogActor?>(() => null, behalfOf => behalfOf.ToActor()));
    }
}
