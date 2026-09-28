using Boilerplate.Application.Common;
using Boilerplate.Application.Users.Commands.ForgotPassword;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using Moq;

namespace Boilerplate.Tests.Application.Users;

public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ForgotPasswordCommandHandler CreateHandler() =>
        new(_userRepository.Object, _emailSender.Object, _unitOfWork.Object);

    private static User CreateUser(DateTime? resetCodeExpiresAt = null)
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create("joao@example.com").Value, "hash").Value;

        if (resetCodeExpiresAt is { } expiresAt)
        {
            user.SetPasswordResetCode("111111", expiresAt);
        }

        return user;
    }

    [Fact]
    public async Task Handle_ComUsuarioExistente_EnviaCodigoEPersiste()
    {
        var user = CreateUser();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new ForgotPasswordCommand("joao@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _emailSender.Verify(
            s => s.SendAsync("joao@example.com", "joao", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ComUsuarioInexistente_RetornaSucessoSemEnviar()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new ForgotPasswordCommand("naoexiste@example.com"), CancellationToken.None);

        // Nao revela se o e-mail existe ou nao.
        Assert.True(result.IsSuccess);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DentroDoCooldown_RetornaFalhaSemEnviar()
    {
        var user = CreateUser(DateTime.UtcNow.AddMinutes(15).AddSeconds(-10));
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new ForgotPasswordCommand("joao@example.com"), CancellationToken.None);

        Assert.True(result.IsFailed);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ComEmailInvalido_RetornaSucessoSemEnviar()
    {
        var result = await CreateHandler().Handle(new ForgotPasswordCommand("nao-e-email"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
