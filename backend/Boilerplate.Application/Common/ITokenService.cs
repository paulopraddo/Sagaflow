using Boilerplate.Domain.Users;

namespace Boilerplate.Application.Common;

public interface ITokenService
{
    string GenerateToken(User user);
}
