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
        builder.HasKey(link => link.UserId);
        builder.Property(link => link.UserId).ValueGeneratedNever();
        builder.HasOne<HumanUser>().WithMany().HasForeignKey(link => link.PrimaryUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HumanUser>().WithMany().HasForeignKey(link => link.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
