using Boilerplate.Domain.Users;

namespace Boilerplate.Tests.Domain.Users;

public class UsernameTests
{
    [Theory]
    [InlineData("joao")]
    [InlineData("joao.silva")]
    [InlineData("joao_silva123")]
    public void Create_ComUsernameValido_RetornaSucesso(string value)
    {
        var result = Username.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Fact]
    public void Create_ComMenosDoQueOMinimo_RetornaFalha()
    {
        var result = Username.Create("ab");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_ComMaisDoQueOMaximo_RetornaFalha()
    {
        var result = Username.Create(new string('a', Username.MaxLength + 1));

        Assert.True(result.IsFailed);
    }

    [Theory]
    [InlineData("joao silva")]
    [InlineData("joao@silva")]
    [InlineData("joão")]
    [InlineData("joao!")]
    public void Create_ComCaracteresInvalidos_RetornaFalha(string value)
    {
        var result = Username.Create(value);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_RemoveEspacosNasExtremidades()
    {
        var result = Username.Create("  joao  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("joao", result.Value.Value);
    }
}
