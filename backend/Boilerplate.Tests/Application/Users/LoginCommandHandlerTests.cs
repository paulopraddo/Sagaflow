using Boilerplate.Application.Common;
using Boilerplate.Application.Users.Commands.Login;
using Boilerplate.Domain.Users;
using Moq;

namespace Boilerplate.Tests.Application.Users;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();

    private LoginCommandHandler CreateHandler() =>
        new(_userRepository.Object, _passwordHasher.Object, _tokenService.Object);

    private static User CreateVerifiedUser(string email = "joao@example.com", string passwordHash = "hash")
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create(email).Value, passwordHash).Value;
        user.SetVerificationCode("123456", DateTime.UtcNow.AddMinutes(15));
        user.VerifyEmail("123456");
        return user;
    }

    [Fact]
    public async Task Handle_ComCredenciaisValidasEEmailVerificado_RetornaToken()
    {
        var user = CreateVerifiedUser();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("senha1234", "hash")).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(user)).Returns("um-token");

        var result = await CreateHandler().Handle(new LoginCommand("joao@example.com", "senha1234"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("um-token", result.Value.Token);
    }

    [Fact]
    public async Task Handle_ComEmailInexistente_RetornaFalha()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new LoginCommand("naoexiste@example.com", "senha1234"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ComSenhaErrada_RetornaFalha()
    {
        var user = CreateVerifiedUser();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await CreateHandler().Handle(new LoginCommand("joao@example.com", "senhaerrada"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ComEmailNaoVerificado_RetornaFalhaSemGerarToken()
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create("joao@example.com").Value, "hash").Value;
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var result = await CreateHandler().Handle(new LoginCommand("joao@example.com", "senha1234"), CancellationToken.None);

        Assert.True(result.IsFailed);
        _tokenService.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ComEmailComFormatoInvalido_RetornaFalha()
    {
        var result = await CreateHandler().Handle(new LoginCommand("nao-e-email", "senha1234"), CancellationToken.None);

        Assert.True(result.IsFailed);
        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
