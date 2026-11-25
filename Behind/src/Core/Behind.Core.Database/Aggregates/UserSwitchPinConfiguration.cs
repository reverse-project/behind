using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserSwitchPinConfiguration : IEntityTypeConfiguration<UserSwitchPin>
{
    public void Configure(EntityTypeBuilder<UserSwitchPin> builder)
    {
        builder.ToTable("UserSwitchPins");
        builder.Property<Guid>("UserId").ValueGeneratedNever();
        builder.HasKey("UserId");
        builder.HasOne(pin => pin.User).WithMany().HasForeignKey("UserId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Property(pin => pin.PinHash).HasConversion(hash => hash.ToString(), value => new PinHash(value)).IsRequired();
    }
}
