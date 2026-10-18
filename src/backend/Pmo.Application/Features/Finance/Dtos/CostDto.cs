namespace Pmo.Application.Features.Finance.Dtos;

public record CostDto(
    long Id, int ProjectId, int CategoryId, string CategoryName, DateOnly TxnDate, decimal Amount, string Description,
    string? VendorName, string? ReferenceNo, string Status, long? ReversesTransactionId, bool IsReversed,
    string CreatedByName, string? PostedByName, DateTimeOffset? PostedAt, uint Version);
