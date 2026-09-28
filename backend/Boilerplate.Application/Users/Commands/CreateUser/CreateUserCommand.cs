using Boilerplate.Application.Common;
using FluentResults;
using MediatR;

namespace Boilerplate.Application.Users.Commands.CreateUser;

public sealed record CreateUserCommand(string Username, string Email, string Password) : IRequest<Result<RegisterResult>>;
