using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserPasskeyConfiguration : IEntityTypeConfiguration<UserPasskey>
{
    public const string UserIdProperty = "UserId";

    public void Configure(EntityTypeBuilder<UserPasskey> builder)
    {
        builder.ToTable("UserPasskeys");

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(passkey => passkey.Name).HasMaxLength(UserPasskey.MaxNameLength).IsRequired();
        builder.Property(passkey => passkey.CredentialId).HasMaxLength(UserPasskey.MaxCredentialIdLength).IsRequired();
        builder.Property(passkey => passkey.PublicKey).HasMaxLength(UserPasskey.MaxPublicKeyLength).IsRequired();
        builder.Property(passkey => passkey.SignCount).IsRequired();

        builder.HasIndex(passkey => passkey.CredentialId).IsUnique();

        builder.Property<Guid>(UserIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(UserIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(UserIdProperty);
    }
}
