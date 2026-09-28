using Boilerplate.Domain.Users;

namespace Boilerplate.Tests.Domain.Users;

public class PasswordTests
{
    [Fact]
    public void Create_ComSenhaValida_RetornaSucesso()
    {
        var result = Password.Create("senha1234");

        Assert.True(result.IsSuccess);
        Assert.Equal("senha1234", result.Value.Value);
    }

    [Fact]
    public void Create_ComExatamenteOMinimo_RetornaSucesso()
    {
        var result = Password.Create(new string('a', Password.MinLength));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData(null)]
    public void Create_ComSenhaCurtaDemais_RetornaFalha(string? value)
    {
        var result = Password.Create(value!);

        Assert.True(result.IsFailed);
    }
}
