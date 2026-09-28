using Boilerplate.Application.Common;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResult>>;
