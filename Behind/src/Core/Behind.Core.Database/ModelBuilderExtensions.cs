using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class ModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public void ApplyRemoveDiscriminatorPatch<T>() where T : notnull => modelBuilder.Ignore<T>();
    }
}