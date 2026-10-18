using Pmo.Application.Common;

namespace Pmo.Api.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>Everything needs a signed-in user unless it says [AllowAnonymous]; the named policies add role checks.</summary>
    public static IServiceCollection AddPmoAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.ManageProjects, p => p.RequireRole(Roles.ProjectManager, Roles.Administrator))
            .AddPolicy(Policies.CanDecide, p => p.RequireRole(Roles.ProjectManager, Roles.Administrator))
            .AddPolicy(Policies.CanPostCosts, p => p.RequireRole(Roles.ProjectManager, Roles.Administrator))
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Administrator));
        services.AddScoped<Pmo.Application.Common.Abstractions.ICurrentUser, CurrentUser>();
        return services;
    }
}
