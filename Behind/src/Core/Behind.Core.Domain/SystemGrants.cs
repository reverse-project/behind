using System.Collections;

namespace Behind.Core.Domain;

sealed class SystemGrants : IEnumerable<Grant>
{
    public IEnumerator<Grant> GetEnumerator()
    {
        return Enumerable.Empty<Grant>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static SystemGrants Get() => new();
}