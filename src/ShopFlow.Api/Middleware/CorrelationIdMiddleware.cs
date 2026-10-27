using System.Text.RegularExpressions;
using Serilog.Context;

namespace ShopFlow.Api.Middleware;

/// <summary>
/// Ensures every request has a correlation id. It is taken from the X-Correlation-Id request header
/// (when well-formed) or generated, echoed back in the response, attached to every log line of the
/// request and stored on every audit row written by it. One id ties a request's logs and audit trail together.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Never trust client input blindly: it ends up in logs and in the audit table.
    private static bool IsValid(string value) => value.Length is > 0 and <= 64 && SafeId().IsMatch(value);

    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex SafeId();
}
