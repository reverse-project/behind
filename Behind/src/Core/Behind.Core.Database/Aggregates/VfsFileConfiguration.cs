using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class VfsFileConfiguration : IEntityTypeConfiguration<VfsFile>
{
    public void Configure(EntityTypeBuilder<VfsFile> builder)
    {
        builder.ToTable("VfsFiles");
        builder.HasVersionedKey();

        builder.Property(file => file.Type).HasMaxLength(VfsFile.MaxTypeLength).IsRequired();
        builder.Property(file => file.FileName).HasMaxLength(VfsFile.MaxFileNameLength).IsRequired();
        builder.Property(file => file.ContentType).HasMaxLength(VfsFile.MaxContentTypeLength).IsRequired();
        builder.Property(file => file.Length).IsRequired();
        builder.Property(file => file.ParentId).IsRequired(false);

        builder.HasIndex(file => file.Type);
        builder.HasIndex(file => file.ParentId);
    }
}
