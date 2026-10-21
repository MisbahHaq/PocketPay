using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).IsRequired().HasMaxLength(320);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PasswordHash).IsRequired();

        // Stored as text so the DB stays readable and safe against enum reordering.
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(user => user.LockoutEndUtc);
        builder.Property(user => user.CreatedAtUtc).IsRequired();
    }
}
