using Boilerplate.Application.Users.Commands.ResetPassword;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using Moq;

namespace Boilerplate.Tests.Application.Users;

public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ResetPasswordCommandHandler CreateHandler() =>
        new(_userRepository.Object, _passwordHasher.Object, _unitOfWork.Object);

    private static User CreateUserWithResetCode(string code = "123456")
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create("joao@example.com").Value, "hash-antigo").Value;
        user.SetPasswordResetCode(code, DateTime.UtcNow.AddMinutes(15));
        return user;
    }

    [Fact]
    public async Task Handle_ComCodigoCorreto_TrocaSenhaEPersiste()
    {
        var user = CreateUserWithResetCode();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Hash("novaSenha123")).Returns("hash-novo");

        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("joao@example.com", "123456", "novaSenha123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-novo", user.PasswordHash);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ComCodigoErrado_RetornaFalhaSemSalvar()
    {
        var user = CreateUserWithResetCode();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("joao@example.com", "000000", "novaSenha123"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("hash-antigo", user.PasswordHash);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ComUsuarioInexistente_RetornaFalha()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("naoexiste@example.com", "123456", "novaSenha123"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ComSenhaInvalida_RetornaFalhaSemConsultarUsuario()
    {
        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("joao@example.com", "123456", "curta"), CancellationToken.None);

        Assert.True(result.IsFailed);
        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SemCodigoPendente_RetornaFalha()
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create("joao@example.com").Value, "hash").Value;
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("joao@example.com", "123456", "novaSenha123"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
