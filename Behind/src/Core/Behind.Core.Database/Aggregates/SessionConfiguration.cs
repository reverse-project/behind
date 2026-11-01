using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class SessionConfiguration : IEntityTypeConfiguration<ValidSession>
{
    public void Configure(EntityTypeBuilder<ValidSession> builder)
    {
        builder.ToTable("Sessions");

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(s => s.RemoteAddress)
            .HasConversion(a => a.Value, v => new RemoteAddress(v))
            .HasMaxLength(RemoteAddress.MaxLength)
            .IsRequired();

        builder.Ignore(s => s.UserAgent);
        builder.Property<Guid>("UserAgentId").IsRequired();
        builder.HasOne<UserAgent>()
            .WithMany()
            .HasForeignKey("UserAgentId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(s => s.AccessToken, token =>
        {
            token.Ignore(t => t.User);
            token.Property(t => t.Nonce).HasConversion(n => n.Nonce, g => new TokenNonce(g));
            token.Property(t => t.IssuedAt).HasConversion<DateTimeOffsetConverter>();
            token.Property(t => t.ExpiresAt).HasConversion<DateTimeOffsetConverter>();
        });

        builder.ComplexProperty(s => s.RefreshToken, token =>
        {
            token.Ignore(t => t.User);
            token.Property(t => t.Nonce).HasConversion(n => n.Nonce, g => new TokenNonce(g));
            token.Property(t => t.IssuedAt).HasConversion<DateTimeOffsetConverter>();
            token.Property(t => t.ExpiresAt).HasConversion<DateTimeOffsetConverter>();
        });

        builder.Ignore(s => s.User);
        builder.Property<Guid>("UserId").IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.Impersonator);
        builder.Property<Guid?>("ImpersonatorId").IsRequired(false);
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey("ImpersonatorId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property<Guid?>("InvalidatedById").IsRequired(false);
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey("InvalidatedById")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property<DateTimeOffset?>("InvalidatedAt")
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);

        builder.HasIndex("UserId");
        builder.HasIndex("ImpersonatorId");
        builder.HasIndex("InvalidatedAt");
    }
}
