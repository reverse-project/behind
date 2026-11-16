namespace Behind.Core.Domain;

public static class AccessNamesExtensions
{
    extension(IEnumerable<Grant> grants)
    {
        public IReadOnlyList<string> Names => grants.Select(grant => grant.Name).ToList();
    }

    extension(IEnumerable<Restriction> restrictions)
    {
        public IReadOnlyList<string> Names => restrictions.Select(restriction => restriction.Name).ToList();
    }
}
