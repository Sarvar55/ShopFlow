using System.ComponentModel.DataAnnotations;

namespace ShopFlow.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = null!;
    [Required] public string Audience { get; init; } = null!;

    /// <summary>HMAC-SHA256 signing key; must be at least 32 bytes.</summary>
    [Required, MinLength(32)] public string SigningKey { get; init; } = null!;

    [Range(1, 1440)] public int ExpiryMinutes { get; init; } = 60;
}
