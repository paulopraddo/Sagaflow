using Boilerplate.Application.Common;
using Boilerplate.Application.Users.Commands.ResendVerificationCode;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using Moq;

namespace Boilerplate.Tests.Application.Users;

public class ResendVerificationCodeCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ResendVerificationCodeCommandHandler CreateHandler() =>
        new(_userRepository.Object, _emailSender.Object, _unitOfWork.Object);

    private static User CreateUnverifiedUser(DateTime? codeExpiresAt = null)
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create("joao@example.com").Value, "hash").Value;

        if (codeExpiresAt is { } expiresAt)
        {
            user.SetVerificationCode("111111", expiresAt);
        }

        return user;
    }

    [Fact]
    public async Task Handle_SemCodigoAnterior_EnviaNovoCodigoEPersiste()
    {
        var user = CreateUnverifiedUser();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new ResendVerificationCodeCommand("joao@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _emailSender.Verify(
            s => s.SendAsync("joao@example.com", "joao", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DentroDoCooldown_RetornaFalhaSemEnviar()
    {
        // codigo emitido a poucos segundos (dentro dos 15 min de validade menos o cooldown de 60s)
        var user = CreateUnverifiedUser(DateTime.UtcNow.AddMinutes(15).AddSeconds(-10));
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new ResendVerificationCodeCommand("joao@example.com"), CancellationToken.None);

        Assert.True(result.IsFailed);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AposCooldown_EnviaNovoCodigo()
    {
        // codigo emitido ha mais de 15 minutos (validade + cooldown ja passaram)
        var user = CreateUnverifiedUser(DateTime.UtcNow.AddMinutes(-1));
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new ResendVerificationCodeCommand("joao@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ComUsuarioInexistente_RetornaSucessoSemEnviar()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new ResendVerificationCodeCommand("naoexiste@example.com"), CancellationToken.None);

        // Nao revela se o e-mail existe ou nao.
        Assert.True(result.IsSuccess);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ComEmailJaVerificado_RetornaSucessoSemEnviar()
    {
        var user = CreateUnverifiedUser(DateTime.UtcNow.AddMinutes(15));
        user.VerifyEmail("111111");
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new ResendVerificationCodeCommand("joao@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _emailSender.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
