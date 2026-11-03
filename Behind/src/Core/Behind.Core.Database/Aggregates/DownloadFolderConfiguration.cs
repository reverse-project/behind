using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class DownloadFolderConfiguration : IEntityTypeConfiguration<DownloadFolder>
{
    public const string ParentIdProperty = "ParentId";

    public void Configure(EntityTypeBuilder<DownloadFolder> builder)
    {
        builder.ToTable("DownloadFolders");
        builder.HasVersionedKey();

        builder.Property(folder => folder.Slug).HasMaxLength(DownloadFolder.MaxSlugLength).IsRequired();
        builder.Property(folder => folder.DisplayName).HasMaxLength(DownloadFolder.MaxDisplayNameLength).IsRequired();

        builder.Property<Guid?>(ParentIdProperty).IsRequired(false);

        builder.HasIndex(ParentIdProperty);
    }
}
