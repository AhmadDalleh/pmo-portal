namespace Pmo.Application.Features.Finance.Commands;

public record UpdateCostCommand(int CategoryId, DateOnly TxnDate, decimal Amount, string Description, string? VendorName, string? ReferenceNo, uint Version);
