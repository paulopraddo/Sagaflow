using Boilerplate.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasConversion(username => username.Value, value => Username.Create(value).Value)
            .HasMaxLength(Username.MaxLength)
            .HasColumnName("username")
            .IsRequired();

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(320)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.IsEmailVerified)
            .HasColumnName("is_email_verified")
            .IsRequired();

        builder.Property(u => u.VerificationCode)
            .HasColumnName("verification_code")
            .HasMaxLength(6);

        builder.Property(u => u.VerificationCodeExpiresAt)
            .HasColumnName("verification_code_expires_at");

        builder.Property(u => u.PasswordResetCode)
            .HasColumnName("password_reset_code")
            .HasMaxLength(6);

        builder.Property(u => u.PasswordResetCodeExpiresAt)
            .HasColumnName("password_reset_code_expires_at");

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
