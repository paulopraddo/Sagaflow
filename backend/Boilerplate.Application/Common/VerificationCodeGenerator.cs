namespace Boilerplate.Application.Common;

public static class VerificationCodeGenerator
{
    public const int ValidityMinutes = 15;

    public static string Generate() => Random.Shared.Next(0, 1_000_000).ToString("D6");
}
