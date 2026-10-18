using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Domain.Common;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Finance;

/// <summary>
/// Story F2: cost categories. The routes that change them are Administrator only. There is no delete:
/// a category that has been used can only be deactivated, which keeps every historic cost valid.
/// </summary>
public sealed class CostCategoryService(
    IPmoDbContext db, ICurrentUser user, TimeProvider clock, ICorrelationIdAccessor correlation,
    IValidator<CreateCategoryCommand> createValidator, IValidator<UpdateCategoryCommand> updateValidator)
{
    public Task<List<CostCategoryDto>> ListAsync(CancellationToken ct) =>
        db.CostCategories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CostCategoryDto(c.Id, c.Code, c.Name, c.IsActive)).ToListAsync(ct);

    public async Task<CostCategoryDto> CreateAsync(CreateCategoryCommand cmd, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(cmd, ct);
        var code = cmd.Code.Trim().ToUpperInvariant();
        var name = cmd.Name.Trim();
        await EnsureUniqueAsync(code, name, null, ct);

        var category = new CostCategory { Code = code, Name = name, IsActive = true };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.CostCategories.Add(category);
        await db.SaveChangesAsync(ct);
        db.AuditEvents.Add(AuditEvent.Create("cost_category.created", "CostCategory", category.Id, user.Id, null, correlation.CorrelationId,
            clock.GetUtcNow(), new { code, name }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CostCategoryDto(category.Id, category.Code, category.Name, category.IsActive);
    }

    public async Task<CostCategoryDto> UpdateAsync(int id, UpdateCategoryCommand cmd, CancellationToken ct)
    {
        var category = await db.CostCategories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Cost category", id);
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        var name = cmd.Name.Trim();
        await EnsureUniqueAsync(category.Code, name, id, ct);

        category.Name = name;
        category.IsActive = cmd.IsActive;
        db.AuditEvents.Add(AuditEvent.Create("cost_category.updated", "CostCategory", id, user.Id, null, correlation.CorrelationId,
            clock.GetUtcNow(), new { name, cmd.IsActive }));
        await db.SaveChangesAsync(ct);
        return new CostCategoryDto(category.Id, category.Code, category.Name, category.IsActive);
    }

    private async Task EnsureUniqueAsync(string code, string name, int? ignoreId, CancellationToken ct)
    {
        if (await db.CostCategories.AnyAsync(c => c.Id != ignoreId && (c.Code == code || c.Name == name), ct))
        {
            throw new DomainRuleException("duplicate_category", "A category with the same code or name already exists.");
        }
    }
}
