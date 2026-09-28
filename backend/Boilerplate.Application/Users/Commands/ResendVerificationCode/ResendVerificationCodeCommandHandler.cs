using Boilerplate.Application.Common;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.ResendVerificationCode;

public sealed class ResendVerificationCodeCommandHandler(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ResendVerificationCodeCommand, Result>
{
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    public async Task<Result> Handle(ResendVerificationCodeCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);

        if (emailResult.IsFailed)
        {
            // Não revela se o e-mail existe ou não.
            return Result.Ok();
        }

        var user = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);

        if (user is null || user.IsEmailVerified)
        {
            return Result.Ok();
        }

        if (user.VerificationCodeExpiresAt is { } expiresAt)
        {
            var issuedAt = expiresAt.AddMinutes(-VerificationCodeGenerator.ValidityMinutes);

            if (DateTime.UtcNow - issuedAt < ResendCooldown)
            {
                return Result.Fail("Aguarde um pouco antes de solicitar um novo código.");
            }
        }

        var code = VerificationCodeGenerator.Generate();
        user.SetVerificationCode(code, DateTime.UtcNow.AddMinutes(VerificationCodeGenerator.ValidityMinutes));

        // Só grava o novo código se o e-mail sair — senão o código antigo (que o usuário
        // ainda não usou) seria substituído por um que ele nunca recebeu.
        await emailSender.SendAsync(
            user.Email.Value,
            user.Username.Value,
            "Seu novo código de confirmação - Boilerplate",
            VerificationEmailTemplate.Render(user.Username.Value, code),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
