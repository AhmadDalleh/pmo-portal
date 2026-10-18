using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Finance;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Application.Features.Finance.Validators;
using Pmo.Application.Features.Lookups;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Application.Features.Milestones;
using Pmo.Application.Features.Milestones.Commands;
using Pmo.Application.Features.Milestones.Validators;
using Pmo.Application.Features.Projects.Validators;
using Pmo.Application.Features.Snapshots;
using Pmo.Application.Features.Tasks;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Application.Features.Tasks.Validators;

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
        services.AddScoped<TaskMapper>();
        services.AddScoped<TaskService>();
        services.AddScoped<MilestoneService>();
        services.AddScoped<SnapshotService>();
        services.AddScoped<CostMapper>();
        services.AddScoped<CostService>();
        services.AddScoped<FinanceQueryService>();
        services.AddScoped<BudgetRevisionService>();
        services.AddScoped<ForecastService>();
        services.AddScoped<CostCategoryService>();

        services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectValidator>();
        services.AddScoped<IValidator<UpdateProjectCommand>, UpdateProjectValidator>();
        services.AddScoped<IValidator<AddMemberCommand>, AddMemberValidator>();
        services.AddScoped<IValidator<ProjectSearchQuery>, ProjectSearchValidator>();

        services.AddScoped<IValidator<CreateTaskCommand>, CreateTaskValidator>();
        services.AddScoped<IValidator<UpdateTaskCommand>, UpdateTaskValidator>();
        services.AddScoped<IValidator<UpdateTaskProgressCommand>, UpdateTaskProgressValidator>();
        services.AddScoped<IValidator<TaskSearchQuery>, TaskSearchValidator>();
        services.AddScoped<IValidator<MyTasksQuery>, MyTasksValidator>();
        services.AddScoped<IValidator<CreateMilestoneCommand>, CreateMilestoneValidator>();
        services.AddScoped<IValidator<UpdateMilestoneCommand>, UpdateMilestoneValidator>();
        services.AddScoped<IValidator<CreateCostCommand>, CreateCostValidator>();
        services.AddScoped<IValidator<UpdateCostCommand>, UpdateCostValidator>();
        services.AddScoped<IValidator<ReverseCostCommand>, ReverseCostValidator>();
        services.AddScoped<IValidator<CostSearchQuery>, CostSearchValidator>();
        services.AddScoped<IValidator<RequestRevisionCommand>, RequestRevisionValidator>();
        services.AddScoped<IValidator<DecideRevisionCommand>, DecideRevisionValidator>();
        services.AddScoped<IValidator<SaveForecastsCommand>, SaveForecastsValidator>();
        services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryValidator>();
        services.AddScoped<IValidator<UpdateCategoryCommand>, UpdateCategoryValidator>();
        return services;
    }
}
