using ShopFlow.Domain.Common;

namespace ShopFlow.Domain.Users;

public enum UserRole
{
    Customer = 0,
    Admin = 1
}

public sealed class User : Entity, IAuditable, IHasTimestamps
{
    private User() { } // EF Core

    public string Email { get; private set; } = null!;

    [AuditIgnore]
    public string PasswordHash { get; private set; } = null!;

    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Register(string email, string passwordHash, string fullName, UserRole role = UserRole.Customer)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email is required.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Full name is required.");

        return new User
        {
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role
        };
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
