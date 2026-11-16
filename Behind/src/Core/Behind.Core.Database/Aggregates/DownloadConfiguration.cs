using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class DownloadConfiguration : IEntityTypeConfiguration<Download>
{
    public const string FolderIdProperty = "FolderId";
    public const string FileIdProperty = "FileId";
    public const string UploadedByIdProperty = "UploadedById";

    public void Configure(EntityTypeBuilder<Download> builder)
    {
        builder.ToTable("Downloads");
        builder.HasVersionedKey();

        builder.Property(download => download.DisplayName).HasMaxLength(Download.MaxDisplayNameLength).IsRequired();

        builder.Property<Guid>(FolderIdProperty).IsRequired();
        builder.Property<Guid>(FileIdProperty).IsRequired();

        builder.Property<Guid>(UploadedByIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(UploadedByIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(FolderIdProperty);
        builder.HasIndex(FileIdProperty);
    }
}
