using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class AccessSettingsConfiguration : IEntityTypeConfiguration<AccessSettings>
{
    public void Configure(EntityTypeBuilder<AccessSettings> builder)
    {
        builder.ToTable("AccessSettings");
        builder.HasVersionedKey();

        builder.Property(settings => settings.RestrictionEnabled);
        builder.Property(settings => settings.RestrictedToRole).HasMaxLength(AccessSettings.MaxRoleLength).IsRequired();
        builder.Property(settings => settings.RestrictionTitle).HasMaxLength(AccessSettings.MaxTitleLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(settings => settings.RestrictionMessage).HasMaxLength(AccessSettings.MaxMessageLength).IsRequired();
        builder.Property(settings => settings.ModeratedSignUp);
    }
}
