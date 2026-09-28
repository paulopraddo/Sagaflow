using Boilerplate.Domain.Common;
using FluentResults;

namespace Boilerplate.Domain.Users;

public sealed class User : Entity
{
    public Username Username { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedAt { get; }
    public bool IsEmailVerified { get; private set; }
    public string? VerificationCode { get; private set; }
    public DateTime? VerificationCodeExpiresAt { get; private set; }
    public string? PasswordResetCode { get; private set; }
    public DateTime? PasswordResetCodeExpiresAt { get; private set; }

    private User(Guid id, Username username, Email email, string passwordHash, DateTime createdAt)
        : base(id)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        IsEmailVerified = false;
    }

    public static Result<User> Create(Username username, Email email, string passwordHash)
    {
        return Result.Ok(new User(Guid.NewGuid(), username, email, passwordHash, DateTime.UtcNow));
    }

    public void ChangeUsername(Username username)
    {
        Username = username;
    }

    public void SetVerificationCode(string code, DateTime expiresAt)
    {
        VerificationCode = code;
        VerificationCodeExpiresAt = expiresAt;
    }

    public Result VerifyEmail(string code)
    {
        if (IsEmailVerified)
        {
            return Result.Ok();
        }

        if (VerificationCode is null || VerificationCodeExpiresAt is null)
        {
            return Result.Fail("Nenhum código de verificação pendente. Solicite um novo.");
        }

        if (DateTime.UtcNow > VerificationCodeExpiresAt)
        {
            return Result.Fail("O código expirou. Solicite um novo.");
        }

        if (!string.Equals(VerificationCode, code, StringComparison.Ordinal))
        {
            return Result.Fail("Código inválido.");
        }

        IsEmailVerified = true;
        VerificationCode = null;
        VerificationCodeExpiresAt = null;
        return Result.Ok();
    }

    public void SetPasswordResetCode(string code, DateTime expiresAt)
    {
        PasswordResetCode = code;
        PasswordResetCodeExpiresAt = expiresAt;
    }

    public Result ResetPassword(string code, string newPasswordHash)
    {
        if (PasswordResetCode is null || PasswordResetCodeExpiresAt is null)
        {
            return Result.Fail("Código inválido ou expirado.");
        }

        if (DateTime.UtcNow > PasswordResetCodeExpiresAt)
        {
            return Result.Fail("Código inválido ou expirado.");
        }

        if (!string.Equals(PasswordResetCode, code, StringComparison.Ordinal))
        {
            return Result.Fail("Código inválido ou expirado.");
        }

        PasswordHash = newPasswordHash;
        PasswordResetCode = null;
        PasswordResetCodeExpiresAt = null;
        return Result.Ok();
    }
}
