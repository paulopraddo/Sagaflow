using System.Text.RegularExpressions;
using Boilerplate.Domain.Common;
using FluentResults;

namespace Boilerplate.Domain.Users;

public sealed partial class Username : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    public string Value { get; }

    private Username(string value)
    {
        Value = value;
    }

    public static Result<Username> Create(string value)
    {
        value = value?.Trim() ?? string.Empty;

        if (value.Length is < MinLength or > MaxLength)
        {
            return Result.Fail($"O nome de usuário deve ter entre {MinLength} e {MaxLength} caracteres.");
        }

        if (!ValidCharactersRegex().IsMatch(value))
        {
            return Result.Fail("O nome de usuário só pode conter letras, números, pontos e sublinhados.");
        }

        return Result.Ok(new Username(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-zA-Z0-9._]+$")]
    private static partial Regex ValidCharactersRegex();
}
