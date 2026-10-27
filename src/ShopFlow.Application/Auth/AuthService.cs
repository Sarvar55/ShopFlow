using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Common;
using ShopFlow.Domain.Users;

namespace ShopFlow.Application.Auth;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<AuthService> logger)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = User.Register(email, passwordHasher.Hash(request.Password), request.FullName);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            // Lost a race against a concurrent registration with the same email.
            throw new ConflictException("An account with this email already exists.");
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same message for unknown email and wrong password, so accounts cannot be enumerated.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt");
            throw new UnauthorizedException("Invalid email or password.");
        }

        logger.LogInformation("User {UserId} logged in", user.Id);
        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = tokenService.CreateToken(user);
        return new AuthResponse(user.Id, user.Email, user.Role.ToString(), token, expiresAt);
    }
}
