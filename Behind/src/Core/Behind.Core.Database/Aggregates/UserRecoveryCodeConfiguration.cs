using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserRecoveryCodeConfiguration : IEntityTypeConfiguration<UserRecoveryCode>
{
    public const string UserIdProperty = "UserId";

    public void Configure(EntityTypeBuilder<UserRecoveryCode> builder)
    {
        builder.ToTable("UserRecoveryCodes");

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(code => code.Hash).HasMaxLength(UserRecoveryCode.MaxHashLength).IsRequired();

        builder.Property<Guid>(UserIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(UserIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(UserIdProperty);
    }
}
