using System.Globalization;
using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class AuditLogResponseExtensions
{
    private const string OwnerProperty = "Owner";
    private const string ParentProperty = "Parent";

    extension(User user)
    {
        public AuditLogActor ToActor(Guid? id = null) =>
            user switch
            {
                HumanUser human => new AuditLogActor(
                    nameof(HumanUser),
                    id.HasValue ? id.Value.ToUserLabel(human.DisplayName) : human.DisplayName,
                    human.Emails.Select(email => email.Value).ToList()) { Id = id },
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

    extension(AuditLogChange change)
    {
        public AuditLogChange Labeled(AuditLabels labels) =>
            change with { OldValue = labels.LabelOrNull(change.OldValue), NewValue = labels.LabelOrNull(change.NewValue) };
    }

    extension(object? value)
    {
        private string? ToDisplayValue() => value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    extension(AuditLogEntry entry)
    {
        public AuditLogItem ToItem(AuditLabels labels) =>
            new(
                entry.Timestamp,
                entry.EntityName,
                entry.EntityKey,
                labels.LabelKey(entry.EntityKey),
                entry.Action.GetType().Name,
                entry.Action.ToChanges().Select(change => change.Labeled(labels)).ToList(),
                entry.User.ToActor(entry.ActorId),
                entry.BehalfOf.Match<AuditLogActor?>(() => null, behalfOf => behalfOf.ToActor(entry.BehalfOfActorId)));
    }
}
