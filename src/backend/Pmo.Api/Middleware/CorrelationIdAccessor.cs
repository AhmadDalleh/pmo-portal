using Pmo.Application.Common.Abstractions;

namespace Pmo.Api.Middleware;

/// <summary>Reads the id CorrelationIdMiddleware stored on the current HttpContext.</summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;
}
