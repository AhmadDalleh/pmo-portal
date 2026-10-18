using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Risks.Commands;
using Pmo.Application.Features.Risks.Dtos;
using Pmo.Application.Features.Risks.Queries;
using Pmo.Domain.Common;
using Pmo.Domain.Delivery;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Risks;

/// <summary>Epic D: record and edit risks and issues, and the high-exposure view across the caller's projects.</summary>
public sealed class RiskService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IUserDirectory users, TimeProvider clock, ICorrelationIdAccessor correlation,
    IValidator<CreateRiskCommand> createValidator, IValidator<UpdateRiskCommand> updateValidator, IValidator<RiskSearchQuery> searchValidator)
{
    /// <summary>D2. Across every project the caller can see (or one project).</summary>
    public async Task<PagedResult<RiskDto>> SearchAsync(RiskSearchQuery query, CancellationToken ct)
    {
        await searchValidator.ValidateAndThrowAsync(query, ct);
        var projectIds = await scope.AccessibleProjectIdsAsync(ct);

        var risks = db.Risks.AsNoTracking().Where(r => projectIds.Contains(r.ProjectId));
        if (query.ProjectId is not null) risks = risks.Where(r => r.ProjectId == query.ProjectId);
        risks = (query.Status ?? "open") switch
        {
            "open" => risks.Where(r => r.Status == RiskStatus.Open || r.Status == RiskStatus.Mitigating),
            "closed" => risks.Where(r => r.Status == RiskStatus.Closed),
            _ => risks
        };
        if (query.HighExposure == true) risks = risks.Where(r => r.Score >= Risk.HighExposureThreshold);
        if (query.Probability is not null) risks = risks.Where(r => r.Probability == query.Probability);
        if (query.Impact is not null) risks = risks.Where(r => r.Impact == query.Impact);

        var total = await risks.CountAsync(ct);
        var page = await risks.OrderByDescending(r => r.Score).ThenBy(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<RiskDto>(await ToDtosAsync(page, ct), query.Page, query.PageSize, total);
    }

    public async Task<List<RiskDto>> ListForProjectAsync(int projectId, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        var risks = await db.Risks.AsNoTracking().Where(r => r.ProjectId == projectId).OrderByDescending(r => r.Score).ThenBy(r => r.Id).ToListAsync(ct);
        return await ToDtosAsync(risks, ct);
    }

    public async Task<RiskDto> CreateAsync(int projectId, CreateRiskCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        await createValidator.ValidateAndThrowAsync(cmd, ct);
        await EnsureOwnerIsMemberAsync(projectId, cmd.OwnerId, ct);

        var now = clock.GetUtcNow();
        var risk = new Risk
        {
            ProjectId = projectId, Type = cmd.Type, Title = cmd.Title.Trim(), Description = cmd.Description?.Trim(), OwnerId = cmd.OwnerId,
            Probability = (short)cmd.Probability, Impact = (short)cmd.Impact, Mitigation = cmd.Mitigation?.Trim(), DueDate = cmd.DueDate,
            CreatedAt = now, CreatedById = user.Id
        };
        risk.ApplyStatus(cmd.Status, now);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Risks.Add(risk);
        await db.SaveChangesAsync(ct);   // PostgreSQL computes risk.Score here
        db.AuditEvents.Add(AuditEvent.Create("risk.created", "Risk", risk.Id, user.Id, projectId, correlation.CorrelationId, now,
            new { risk.Title, risk.Score, risk.Status }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ToDtosAsync([risk], ct)).Single();
    }

    public async Task<RiskDto> UpdateAsync(int riskId, UpdateRiskCommand cmd, CancellationToken ct)
    {
        var risk = await db.Risks.FirstOrDefaultAsync(r => r.Id == riskId, ct) ?? throw new NotFoundException("Risk", riskId);
        await scope.EnsureCanManageAsync(risk.ProjectId, ct);
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        await EnsureOwnerIsMemberAsync(risk.ProjectId, cmd.OwnerId, ct);
        db.Entry(risk).Property(r => r.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        var before = new { risk.Status, risk.Probability, risk.Impact };
        risk.Type = cmd.Type;
        risk.Title = cmd.Title.Trim();
        risk.Description = cmd.Description?.Trim();
        risk.OwnerId = cmd.OwnerId;
        risk.Probability = (short)cmd.Probability;
        risk.Impact = (short)cmd.Impact;
        risk.Mitigation = cmd.Mitigation?.Trim();
        risk.DueDate = cmd.DueDate;
        risk.ApplyStatus(cmd.Status, now);   // closing stamps closed_at, reopening clears it
        risk.UpdatedAt = now;
        risk.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("risk.updated", "Risk", risk.Id, user.Id, risk.ProjectId, correlation.CorrelationId, now,
            new { before, after = new { risk.Status, risk.Probability, risk.Impact } }));
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([risk], ct)).Single();
    }

    private async Task EnsureOwnerIsMemberAsync(int projectId, int ownerId, CancellationToken ct)
    {
        if (!await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == ownerId, ct))
        {
            throw new DomainRuleException("owner_not_member", "The risk owner must be a member of the project.", isValidation: true);
        }
    }

    private async Task<List<RiskDto>> ToDtosAsync(IReadOnlyCollection<Risk> risks, CancellationToken ct)
    {
        var projectIds = risks.Select(r => r.ProjectId).Distinct().ToArray();
        var codes = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var owners = await users.NamesAsync(risks.Select(r => r.OwnerId), ct);

        return risks.Select(r => new RiskDto(
            r.Id, r.ProjectId, codes[r.ProjectId], r.Type, r.Title, r.Description, r.OwnerId, owners[r.OwnerId], r.Probability, r.Impact,
            r.Score, r.IsHighExposure, r.Mitigation, r.DueDate, r.Status, r.ClosedAt, r.Version)).ToList();
    }
}
