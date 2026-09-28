using Boilerplate.Application.Common;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);

        if (emailResult.IsFailed)
        {
            // Não revela se o e-mail existe ou não.
            return Result.Ok();
        }

        var user = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);

        if (user is null)
        {
            return Result.Ok();
        }

        if (user.PasswordResetCodeExpiresAt is { } expiresAt)
        {
            var issuedAt = expiresAt.AddMinutes(-VerificationCodeGenerator.ValidityMinutes);

            if (DateTime.UtcNow - issuedAt < ResendCooldown)
            {
                return Result.Fail("Aguarde um pouco antes de solicitar um novo código.");
            }
        }

        var code = VerificationCodeGenerator.Generate();
        user.SetPasswordResetCode(code, DateTime.UtcNow.AddMinutes(VerificationCodeGenerator.ValidityMinutes));

        // Só grava o código se o e-mail sair — senão o usuário nunca recebe o código
        // mas o anterior (que talvez ainda funcione) já teria sido substituído.
        await emailSender.SendAsync(
            user.Email.Value,
            user.Username.Value,
            "Redefinição de senha - Boilerplate",
            PasswordResetEmailTemplate.Render(user.Username.Value, code),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
