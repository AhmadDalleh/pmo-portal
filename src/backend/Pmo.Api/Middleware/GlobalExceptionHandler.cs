using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = FieldErrors(validation);
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    /// <summary>Field name (camelCase, as the Angular forms use it) to its messages.</summary>
    private static Dictionary<string, string[]> FieldErrors(ValidationException validation) => validation.Errors
        .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    private static (int Status, string Code, string Title, string? Detail) Map(Exception exception) => exception switch
    {
        ValidationException => (400, "validation_failed", "Validation failed", "One or more fields are invalid."),
        AuthenticationFailedException e => (401, "invalid_credentials", "Unauthorized", e.Message),
        NotFoundException e => (404, "not_found", "Not found", e.Message),
        ForbiddenException e => (403, "forbidden", "Forbidden", e.Message),
        DomainRuleException { IsForbidden: true } e => (403, e.Code, "Forbidden", e.Message),
        DomainRuleException { IsValidation: true } e => (400, e.Code, "Invalid request", e.Message),
        DomainRuleException e => (409, e.Code, "Business rule violated", e.Message),
        DbUpdateConcurrencyException => (409, "concurrency_conflict", "Conflict",
            "The record was changed by someone else. Reload and try again."),
        // Safety net for a race on a UNIQUE index (the services pre-check, but two requests can still collide).
        DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } =>
            (409, "duplicate_value", "Conflict", "A record with the same unique value already exists."),
        // Unknown errors: never leak the message or stack trace to the client.
        _ => (500, "internal_error", "Unexpected error", "Something went wrong. Quote the correlation id when reporting it.")
    };
}
