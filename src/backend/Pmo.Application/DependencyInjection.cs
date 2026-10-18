using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Lookups;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Application.Features.Projects.Validators;

namespace Pmo.Application;

public static class DependencyInjection
{
    /// <summary>Registers services and validators one by one (no assembly scanning), so the list doubles as a feature index.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IProjectScope, ProjectScope>();

        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectMemberService>();
        services.AddScoped<LookupService>();

        services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectValidator>();
        services.AddScoped<IValidator<UpdateProjectCommand>, UpdateProjectValidator>();
        services.AddScoped<IValidator<AddMemberCommand>, AddMemberValidator>();
        services.AddScoped<IValidator<ProjectSearchQuery>, ProjectSearchValidator>();
        return services;
    }
}
