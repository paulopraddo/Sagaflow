using Boilerplate.Application.Common;
using Boilerplate.Domain.Common;
using Boilerplate.Domain.Users;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateUserCommand, Result<RegisterResult>>
{
    public async Task<Result<RegisterResult>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var usernameResult = Username.Create(request.Username);
        var emailResult = Email.Create(request.Email);
        var passwordResult = Password.Create(request.Password);

        if (usernameResult.IsFailed || emailResult.IsFailed || passwordResult.IsFailed)
        {
            return Result.Fail<RegisterResult>(
                usernameResult.Errors.Concat(emailResult.Errors).Concat(passwordResult.Errors));
        }

        if (await userRepository.ExistsByUsernameAsync(usernameResult.Value, cancellationToken))
        {
            return Result.Fail<RegisterResult>("Já existe um usuário com esse nome de usuário.");
        }

        if (await userRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken))
        {
            return Result.Fail<RegisterResult>("Já existe um usuário com esse e-mail.");
        }

        var passwordHash = passwordHasher.Hash(passwordResult.Value.Value);
        var user = User.Create(usernameResult.Value, emailResult.Value, passwordHash).Value;

        var code = VerificationCodeGenerator.Generate();
        user.SetVerificationCode(code, DateTime.UtcNow.AddMinutes(VerificationCodeGenerator.ValidityMinutes));

        // Só grava o usuário se o e-mail de verificação realmente sair — senão a conta
        // fica presa no banco sem o usuário nunca ter recebido o código para confirmar.
        await emailSender.SendAsync(
            user.Email.Value,
            user.Username.Value,
            "Confirme seu e-mail no Boilerplate",
            VerificationEmailTemplate.Render(user.Username.Value, code),
            cancellationToken);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(new RegisterResult(user.Id, user.Email.Value));
    }
}
