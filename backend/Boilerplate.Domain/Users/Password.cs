using Boilerplate.Domain.Common;
using FluentResults;

namespace Boilerplate.Domain.Users;

public sealed class Password : ValueObject
{
    public const int MinLength = 8;

    public string Value { get; }

    private Password(string value)
    {
        Value = value;
    }

    public static Result<Password> Create(string value)
    {
        value ??= string.Empty;

        if (value.Length < MinLength)
        {
            return Result.Fail($"A senha deve ter pelo menos {MinLength} caracteres.");
        }

        return Result.Ok(new Password(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
