namespace Pmo.Application.Features.Finance.Commands;

/// <summary>F5. Amount is always positive; corrections are reversals.</summary>
public record CreateCostCommand(int CategoryId, DateOnly TxnDate, decimal Amount, string Description, string? VendorName, string? ReferenceNo);
