using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance;

/// <summary>Cost rows to DTOs: category and user names plus the "already reversed" flag, loaded in a few small queries.</summary>
public sealed class CostMapper(IPmoDbContext db, IUserDirectory users)
{
    public async Task<List<CostDto>> ToDtosAsync(IReadOnlyCollection<CostTransaction> costs, CancellationToken ct)
    {
        var categoryIds = costs.Select(c => c.CategoryId).Distinct().ToArray();
        var ids = costs.Select(c => c.Id).ToArray();
        var categories = await db.CostCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var reversed = (await db.CostTransactions.AsNoTracking().Where(c => c.ReversesTransactionId != null && ids.Contains(c.ReversesTransactionId.Value))
            .Select(c => c.ReversesTransactionId!.Value).ToListAsync(ct)).ToHashSet();
        var people = await users.NamesAsync(costs.Select(c => c.CreatedById).Concat(costs.Where(c => c.PostedById != null).Select(c => c.PostedById!.Value)), ct);

        return costs.Select(c => new CostDto(
            c.Id, c.ProjectId, c.CategoryId, categories[c.CategoryId], c.TxnDate, c.Amount, c.Description, c.VendorName, c.ReferenceNo,
            c.Status, c.ReversesTransactionId, reversed.Contains(c.Id), people[c.CreatedById],
            c.PostedById is { } p ? people[p] : null, c.PostedAt, c.Version)).ToList();
    }
}
