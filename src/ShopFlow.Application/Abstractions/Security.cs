using ShopFlow.Domain.Users;

namespace ShopFlow.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
}

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
