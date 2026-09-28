using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;
