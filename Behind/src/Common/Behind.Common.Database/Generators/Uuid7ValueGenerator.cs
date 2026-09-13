using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Behind.Common.Database.Generators;

public sealed class Uuid7ValueGenerator : ValueGenerator<Guid>
{
    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();

    public override bool GeneratesTemporaryValues => false;
}