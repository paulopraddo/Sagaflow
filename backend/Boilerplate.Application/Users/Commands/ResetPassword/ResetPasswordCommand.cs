using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword) : IRequest<Result>;
