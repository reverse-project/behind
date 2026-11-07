namespace Behind.Core.Domain;

public static class AccessSelectionExtensions
{
    extension(BuiltInRole builtIn)
    {
        public AccessSelection DefaultSelection(Role current) =>
            new(builtIn.Grants.Select(grant => grant.Grant.Name).ToList(), current.RestrictionNames);
    }

    extension(AccessSelection selection)
    {
        public AccessSelection Normalized =>
            new(
                selection.Grants.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                selection.Restrictions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
    }
}
