using Behind.Common;

namespace Behind.Core.Domain;

public sealed record ContentAgeOption(VersionedKey<Guid> Id, int Age, string Name)
{
    public ContentAgeOption(int age, string name) : this(new VersionedKey<Guid>(Guid.Empty), age, name)
    {
    }
}
