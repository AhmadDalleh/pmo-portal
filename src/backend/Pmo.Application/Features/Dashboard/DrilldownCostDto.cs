namespace Pmo.Application.Features.Dashboard;

public record DrilldownCostDto(
    long Id, string ProjectCode, string CategoryName, DateOnly TxnDate, decimal Amount, string Description, string? VendorName, string? ReferenceNo);
