using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Api.Features.Users;

namespace Web.Api.Database.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.ExternalId).HasMaxLength(64).IsRequired();
        builder.HasIndex(u => u.ExternalId).IsUnique();

        // Not unique: Clerk owns emails, and an address can move between Clerk accounts.
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email);

        // Enums as text: readable in SQL and safe to reorder in code.
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16).HasDefaultValue(Role.Learner).HasSentinel(Role.Learner);
        builder.Property(u => u.Plan).HasConversion<string>().HasMaxLength(16).HasDefaultValue(Plan.Free).HasSentinel(Plan.Free);

        builder.Property(u => u.IsDisabled).HasDefaultValue(false);
        builder.Property(u => u.IsDemo).HasDefaultValue(false);
    }
}
