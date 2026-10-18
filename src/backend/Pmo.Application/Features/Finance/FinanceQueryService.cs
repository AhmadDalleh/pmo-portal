using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Application.Features.Projects;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance;

/// <summary>Stories F1 and F9: budget overview, spend by category, monthly trend. Read only; numbers come from SQL.</summary>
public sealed class FinanceQueryService(
    IPmoDbContext db, IProjectScope scope, IProjectReadModel projects, IFinanceReadModel finance, IOptions<ReportingOptions> reporting)
{
    public async Task<FinanceSummaryDto> SummaryAsync(int projectId, DateOnly? asOf, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        var date = reporting.Value.Resolve(asOf);
        var kpi = await projects.GetKpisAsync(projectId, date, ct) ?? throw new NotFoundException("Project", projectId);

        var initial = await db.ProjectBudgets.AsNoTracking().Where(b => b.ProjectId == projectId).Select(b => b.InitialAmount).FirstAsync(ct);
        var pending = await db.BudgetRevisions.AsNoTracking().CountAsync(r => r.ProjectId == projectId && r.Status == RevisionStatus.Pending, ct);

        return new FinanceSummaryDto(
            projectId, date, "AED", initial, kpi.CurrentBudget - initial, kpi.CurrentBudget, kpi.ActualSpend, kpi.RemainingBudget,
            kpi.Utilization, kpi.ForecastToComplete, kpi.Eac, kpi.ProjectedVariance,
            kpi.NearBudget, kpi.Overspent, kpi.ProjectedOverrun, kpi.NoForecast, kpi.NoBudget, pending);
    }

    public async Task<List<CategorySpendDto>> ByCategoryAsync(int projectId, DateOnly? asOf, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        return await finance.ByCategoryAsync(projectId, reporting.Value.Resolve(asOf), ct);
    }

    public async Task<List<MonthlyPointDto>> MonthlyAsync(int projectId, DateOnly? asOf, int? categoryId, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        return await finance.MonthlyAsync(projectId, reporting.Value.Resolve(asOf), categoryId, ct);
    }
}
