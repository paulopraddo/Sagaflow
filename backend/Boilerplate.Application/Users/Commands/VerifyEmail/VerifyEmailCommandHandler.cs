using Boilerplate.Application.Common;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.VerifyEmail;

public sealed class VerifyEmailCommandHandler(
    IUserRepository userRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<VerifyEmailCommand, Result<AuthResult>>
{
    private const string InvalidRequestMessage = "Não foi possível confirmar o e-mail.";

    public async Task<Result<AuthResult>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);

        if (emailResult.IsFailed)
        {
            return Result.Fail<AuthResult>(InvalidRequestMessage);
        }

        var user = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);

        if (user is null)
        {
            return Result.Fail<AuthResult>(InvalidRequestMessage);
        }

        var verifyResult = user.VerifyEmail(request.Code.Trim());

        if (verifyResult.IsFailed)
        {
            return Result.Fail<AuthResult>(verifyResult.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokenService.GenerateToken(user);
        return Result.Ok(new AuthResult(user.Id, user.Username.Value, token));
    }
}
