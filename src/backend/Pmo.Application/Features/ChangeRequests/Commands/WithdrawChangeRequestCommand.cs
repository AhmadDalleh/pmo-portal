namespace Pmo.Application.Features.ChangeRequests.Commands;

public record WithdrawChangeRequestCommand(string? Reason, uint Version);
