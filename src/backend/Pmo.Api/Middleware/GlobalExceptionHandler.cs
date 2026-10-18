using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Exceptions;
using Pmo.Domain.Common;

namespace Pmo.Api.Middleware;

/// <summary>
/// The one place where exceptions become HTTP responses (RFC 7807 ProblemDetails).
/// correlationId is added to every ProblemDetails in Program.cs (CustomizeProblemDetails).
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title, detail) = Map(exception);

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation("Request failed with {StatusCode} {Code}", status, code);
        }

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;

        httpContext.Response.StatusCode = status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static (int Status, string Code, string Title, string? Detail) Map(Exception exception) => exception switch
    {
        NotFoundException e => (404, "not_found", "Not found", e.Message),
        ForbiddenException e => (403, "forbidden", "Forbidden", e.Message),
        DomainRuleException { IsForbidden: true } e => (403, e.Code, "Forbidden", e.Message),
        DomainRuleException { IsValidation: true } e => (400, e.Code, "Invalid request", e.Message),
        DomainRuleException e => (409, e.Code, "Business rule violated", e.Message),
        DbUpdateConcurrencyException => (409, "concurrency_conflict", "Conflict",
            "The record was changed by someone else. Reload and try again."),
        // Unknown errors: never leak the message or stack trace to the client.
        _ => (500, "internal_error", "Unexpected error", "Something went wrong. Quote the correlation id when reporting it.")
    };
}
