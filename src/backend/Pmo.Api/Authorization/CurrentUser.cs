using System.Security.Claims;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;

namespace Pmo.Api.Authorization;

/// <summary>Reads the caller from the validated JWT claims (sub, role). Throws if used on an anonymous request.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User
        ?? throw new InvalidOperationException("No HTTP context.");

    public int Id => int.Parse(Principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Request is not authenticated."));

    public string Role => Principal.FindFirstValue("role") ?? string.Empty;

    public bool IsAdmin => IsInRole(Roles.Administrator);

    public bool IsInRole(string role) => Principal.IsInRole(role);
}
