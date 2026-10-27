using ShopFlow.Application.Abstractions;

namespace ShopFlow.Application.Common;

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser user) =>
        user.UserId ?? throw new UnauthorizedException("Authentication is required.");
}
