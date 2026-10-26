using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sps.Apim.SelfServe.Infrastructure.Identity;

namespace Sps.Apim.SelfServe.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(
        EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(user => user.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.Property(user => user.ActivatedAtUtc);

        builder.Property(user => user.DisabledAtUtc);

        builder.Property(user => user.UpdatedAtUtc);
    }
}