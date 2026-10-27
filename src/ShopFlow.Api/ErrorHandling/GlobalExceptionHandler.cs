using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Common;
using ShopFlow.Domain.Common;

namespace ShopFlow.Api.ErrorHandling;

/// <summary>Maps exceptions to RFC 9457 problem details responses.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, "Validation failed", e.Message),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, "Unauthorized", e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, "Forbidden", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            InsufficientStockException e => (StatusCodes.Status409Conflict, "Insufficient stock", e.Message),
            InvalidOrderStateException e => (StatusCodes.Status409Conflict, "Invalid order state", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            UniqueConstraintException => (StatusCodes.Status409Conflict, "Conflict", "The resource already exists."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict",
                "The resource was modified by someone else. Reload it and try again."),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.")
        };

        var correlationId = context.Items[Middleware.CorrelationIdMiddleware.ItemKey] as string;
        if (status >= 500)
            logger.LogError(exception, "Unhandled exception on {Method} {Path} (CorrelationId {CorrelationId})",
                context.Request.Method, context.Request.Path, correlationId);
        else
            logger.LogInformation("Request {Method} {Path} failed with {StatusCode}: {Reason} (CorrelationId {CorrelationId})",
                context.Request.Method, context.Request.Path, status, detail, correlationId);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
