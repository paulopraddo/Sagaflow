using Boilerplate.Domain.Users;

namespace Boilerplate.Tests.Domain.Users;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@Example.COM")]
    [InlineData("  user@example.com  ")]
    public void Create_ComEmailValido_RetornaSucesso(string value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("sem-arroba.com")]
    [InlineData("sem-dominio@")]
    [InlineData("com espaco@example.com")]
    public void Create_ComEmailInvalido_RetornaFalha(string? value)
    {
        var result = Email.Create(value!);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_NormalizaParaMinusculo()
    {
        var result = Email.Create("Usuario@Exemplo.COM");

        Assert.Equal("usuario@exemplo.com", result.Value.Value);
    }

    [Fact]
    public void DoisEmails_ComMesmoValor_SaoIguais()
    {
        var a = Email.Create("user@example.com").Value;
        var b = Email.Create("USER@example.com").Value;

        Assert.Equal(a, b);
    }
}
