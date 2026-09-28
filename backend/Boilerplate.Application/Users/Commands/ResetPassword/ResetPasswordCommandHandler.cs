using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    private const string InvalidCodeMessage = "Código inválido ou expirado.";

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        var passwordResult = Password.Create(request.NewPassword);

        if (passwordResult.IsFailed)
        {
            return Result.Fail(passwordResult.Errors);
        }

        if (emailResult.IsFailed)
        {
            return Result.Fail(InvalidCodeMessage);
        }

        var user = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);

        if (user is null)
        {
            return Result.Fail(InvalidCodeMessage);
        }

        var newHash = passwordHasher.Hash(passwordResult.Value.Value);
        var resetResult = user.ResetPassword(request.Code.Trim(), newHash);

        if (resetResult.IsFailed)
        {
            return Result.Fail(resetResult.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
