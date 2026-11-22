using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class SubaccountConfiguration : IEntityTypeConfiguration<Subaccount>
{
    public void Configure(EntityTypeBuilder<Subaccount> builder)
    {
        builder.ToTable("Subaccounts", table => table.HasCheckConstraint(
            "CK_Subaccounts_DifferentUsers", "\"PrimaryUserId\" <> \"UserId\""));
        builder.Property<Guid>("UserId").ValueGeneratedNever();
        builder.HasKey("UserId");
        builder.HasOne(link => link.PrimaryUser).WithMany().HasForeignKey("PrimaryUserId").IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(link => link.User).WithMany().HasForeignKey("UserId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Property(link => link.PinHash).HasConversion(hash => (string?)hash, value => (PinHash?)value);
    }
}
