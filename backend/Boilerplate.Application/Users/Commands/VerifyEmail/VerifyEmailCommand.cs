using Boilerplate.Application.Common;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Email, string Code) : IRequest<Result<AuthResult>>;
