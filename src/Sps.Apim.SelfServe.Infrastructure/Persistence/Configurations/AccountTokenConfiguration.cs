using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sps.Apim.SelfServe.Domain.Entities;

namespace Sps.Apim.SelfServe.Infrastructure.Persistence.Configurations;

public sealed class AccountTokenConfiguration
    : IEntityTypeConfiguration<AccountToken>
{
    public void Configure(
        EntityTypeBuilder<AccountToken> builder)
    {
        builder.ToTable("AccountTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(token => token.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(token => token.CreatedAtUtc)
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => new
        {
            token.UserId,
            token.Type
        });
    }
}