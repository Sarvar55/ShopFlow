namespace ShopFlow.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

public sealed class ConflictException(string message) : Exception(message);

public sealed class ForbiddenException(string message = "You are not allowed to perform this action.") : Exception(message);

public sealed class ValidationException(string message) : Exception(message);

public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>Raised by the persistence layer when a unique constraint is violated.</summary>
public sealed class UniqueConstraintException(string? constraintName, Exception inner)
    : Exception($"Unique constraint '{constraintName}' was violated.", inner)
{
    public string? ConstraintName { get; } = constraintName;
}
