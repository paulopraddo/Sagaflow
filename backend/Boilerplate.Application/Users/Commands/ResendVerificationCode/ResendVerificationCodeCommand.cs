using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.ResendVerificationCode;

public sealed record ResendVerificationCodeCommand(string Email) : IRequest<Result>;
