using Boilerplate.Application.Common;
using Boilerplate.Application.Users.Commands.CreateUser;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using Moq;

namespace Boilerplate.Tests.Application.Users;

public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private CreateUserCommandHandler CreateHandler() =>
        new(_userRepository.Object, _passwordHasher.Object, _emailSender.Object, _unitOfWork.Object);

    private static readonly CreateUserCommand ValidCommand = new("joao", "joao@example.com", "senha1234");

    public CreateUserCommandHandlerTests()
    {
        _userRepository.Setup(r => r.ExistsByUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepository.Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
    }

    [Fact]
    public async Task Handle_ComDadosValidos_EnviaEmailEPersisteUsuario()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("joao@example.com", result.Value.Email);
        _emailSender.Verify(
            s => s.SendAsync("joao@example.com", "joao", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UsuarioCriadoAindaNaoEstaVerificado()
    {
        User? savedUser = null;
        _userRepository.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => savedUser = user);

        var handler = CreateHandler();
        await handler.Handle(ValidCommand, CancellationToken.None);

        Assert.NotNull(savedUser);
        Assert.False(savedUser!.IsEmailVerified);
    }

    [Fact]
    public async Task Handle_QuandoEnvioDeEmailFalha_NaoPersisteUsuario()
    {
        _emailSender
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falha no envio"));

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ValidCommand, CancellationToken.None));

        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ComUsernameJaExistente_RetornaFalhaSemEnviarEmail()
    {
        _userRepository.Setup(r => r.ExistsByUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsFailed);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ComEmailJaExistente_RetornaFalha()
    {
        _userRepository.Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ComSenhaInvalida_RetornaFalhaSemConsultarRepositorio()
    {
        var handler = CreateHandler();
        var command = ValidCommand with { Password = "123" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
