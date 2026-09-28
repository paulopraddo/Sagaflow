using System.Text.RegularExpressions;
using Boilerplate.Domain.Common;
using FluentResults;

namespace Boilerplate.Domain.Users;

public sealed partial class Email : ValueObject
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email> Create(string value)
    {
        value = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value) || !ValidFormatRegex().IsMatch(value))
        {
            return Result.Fail("O e-mail informado é inválido.");
        }

        return Result.Ok(new Email(value.ToLowerInvariant()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex ValidFormatRegex();
}
