using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserTotpConfiguration : IEntityTypeConfiguration<UserTotp>
{
    public const string UserIdProperty = "UserId";

    public void Configure(EntityTypeBuilder<UserTotp> builder)
    {
        builder.ToTable("UserTotps");

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(totp => totp.Secret).HasMaxLength(UserTotp.MaxSecretLength).IsRequired();
        builder.Property(totp => totp.IsConfirmed).IsRequired();
        builder.Property(totp => totp.LastUsedStep).IsRequired();

        builder.Property<Guid>(UserIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(UserIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(UserIdProperty).IsUnique();
    }
}
