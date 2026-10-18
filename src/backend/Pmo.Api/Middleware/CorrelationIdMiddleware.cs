using System.Text.RegularExpressions;
using Serilog.Context;

namespace Pmo.Api.Middleware;

/// <summary>
/// Gives every request a correlation id: reuses a safe incoming X-Correlation-Id or creates one,
/// echoes it on the response, and adds it to every log line written during the request.
/// </summary>
public partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadIncomingId(context) ?? Guid.NewGuid().ToString("N");
        context.Items[ItemKey] = correlationId;

        // OnStarting runs just before headers are sent, so the header survives the
        // exception handler clearing the response on errors.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    /// <summary>Accept the client's id only if it is short and plain, so it cannot inject into logs.</summary>
    private static string? ReadIncomingId(HttpContext context)
    {
        var value = context.Request.Headers[HeaderName].ToString();
        return SafeId().IsMatch(value) ? value : null;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeId();
}
