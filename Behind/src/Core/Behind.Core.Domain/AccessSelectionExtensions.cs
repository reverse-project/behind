namespace Behind.Core.Domain;

public static class AccessSelectionExtensions
{
    extension(AccessSelection selection)
    {
        public AccessSelection Normalized =>
            new(
                selection.Grants.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                selection.Restrictions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());

        public bool IsValid =>
            selection.Grants.Concat(selection.Restrictions).All(BuiltInAccess.IsKnownGrant)
            && !selection.Grants.Intersect(selection.Restrictions, StringComparer.Ordinal).Any();
    }
}
