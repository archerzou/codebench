using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Api.Features.Users;

namespace Web.Api.Database.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rp => new { rp.Role, rp.Permission });

        builder.Property(rp => rp.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(rp => rp.Permission).HasMaxLength(64);
    }
}
