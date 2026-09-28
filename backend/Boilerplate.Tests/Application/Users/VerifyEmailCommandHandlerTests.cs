using Boilerplate.Application.Common;
using Boilerplate.Application.Users.Commands.VerifyEmail;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using Moq;

namespace Boilerplate.Tests.Application.Users;

public class VerifyEmailCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private VerifyEmailCommandHandler CreateHandler() =>
        new(_userRepository.Object, _tokenService.Object, _unitOfWork.Object);

    private static User CreateUnverifiedUser(string code = "123456")
    {
        var user = User.Create(Username.Create("joao").Value, Email.Create("joao@example.com").Value, "hash").Value;
        user.SetVerificationCode(code, DateTime.UtcNow.AddMinutes(15));
        return user;
    }

    [Fact]
    public async Task Handle_ComCodigoCorreto_VerificaEGeraToken()
    {
        var user = CreateUnverifiedUser();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokenService.Setup(t => t.GenerateToken(user)).Returns("um-token");

        var result = await CreateHandler().Handle(new VerifyEmailCommand("joao@example.com", "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("um-token", result.Value.Token);
        Assert.True(user.IsEmailVerified);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ComCodigoErrado_RetornaFalhaSemSalvar()
    {
        var user = CreateUnverifiedUser();
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("joao@example.com", "000000"), CancellationToken.None);

        Assert.True(result.IsFailed);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ComUsuarioInexistente_RetornaFalha()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("naoexiste@example.com", "123456"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
