using System.ComponentModel.DataAnnotations;

namespace ShopFlow.Application.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required, StringLength(200)] string FullName);

public sealed record LoginRequest([Required] string Email, [Required] string Password);

public sealed record AuthResponse(Guid UserId, string Email, string Role, string AccessToken, DateTimeOffset ExpiresAt);
