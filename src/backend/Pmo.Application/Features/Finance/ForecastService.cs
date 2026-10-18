using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Domain.Common;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Finance;

/// <summary>Story F8: the forecast grid (months x categories) of one project, saved as an upsert in one transaction.</summary>
public sealed class ForecastService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IOptions<ReportingOptions> reporting, TimeProvider clock,
    ICorrelationIdAccessor correlation, IValidator<SaveForecastsCommand> validator)
{
    public async Task<List<ForecastDto>> ListAsync(int projectId, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        return await ReadAsync(projectId, ct);
    }

    public async Task<List<ForecastDto>> SaveAsync(int projectId, SaveForecastsCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        await validator.ValidateAndThrowAsync(cmd, ct);

        var firstEditableMonth = FirstOfNextMonth(reporting.Value.DefaultAsOf);
        if (cmd.Items.Any(i => i.PeriodMonth < firstEditableMonth))
        {
            throw new DomainRuleException("forecast_month_not_future", $"Forecasts can only be set from {firstEditableMonth:yyyy-MM} onwards.", isValidation: true);
        }

        if (cmd.Items.GroupBy(i => (i.CategoryId, i.PeriodMonth)).Any(g => g.Count() > 1))
        {
            throw new DomainRuleException("duplicate_forecast_cell", "Each category and month may appear only once.", isValidation: true);
        }

        var categoryIds = cmd.Items.Select(i => i.CategoryId).Distinct().ToArray();
        var active = await db.CostCategories.Where(c => categoryIds.Contains(c.Id) && c.IsActive).CountAsync(ct);
        if (active != categoryIds.Length)
        {
            throw new DomainRuleException("category_not_found", "Every category must exist and be active.", isValidation: true);
        }

        var now = clock.GetUtcNow();
        var existing = await db.FinancialForecasts.Where(f => f.ProjectId == projectId).ToListAsync(ct);
        var cells = existing.ToDictionary(f => (f.CategoryId, f.PeriodMonth));
        foreach (var item in cmd.Items)
        {
            if (cells.TryGetValue((item.CategoryId, item.PeriodMonth), out var row))
            {
                row.Amount = item.Amount;
                row.UpdatedById = user.Id;
                row.UpdatedAt = now;
            }
            else
            {
                db.FinancialForecasts.Add(new FinancialForecast
                {
                    ProjectId = projectId, CategoryId = item.CategoryId, PeriodMonth = item.PeriodMonth,
                    Amount = item.Amount, UpdatedById = user.Id, UpdatedAt = now
                });
            }
        }

        db.AuditEvents.Add(AuditEvent.Create("forecast.saved", "Project", projectId, user.Id, projectId, correlation.CorrelationId, now,
            new { cells = cmd.Items.Count, total = cmd.Items.Sum(i => i.Amount) }));
        await db.SaveChangesAsync(ct);   // every cell and the audit row, or nothing
        return await ReadAsync(projectId, ct);
    }

    private async Task<List<ForecastDto>> ReadAsync(int projectId, CancellationToken ct) =>
        await (from f in db.FinancialForecasts.AsNoTracking()
               join c in db.CostCategories on f.CategoryId equals c.Id
               where f.ProjectId == projectId
               orderby f.PeriodMonth, c.Name
               select new ForecastDto(c.Id, c.Name, f.PeriodMonth, f.Amount)).ToListAsync(ct);

    private static DateOnly FirstOfNextMonth(DateOnly date) => new DateOnly(date.Year, date.Month, 1).AddMonths(1);
}
