namespace Pmo.Application.Features.ChangeRequests.Commands;

/// <summary>E3. Decision is "approve", "reject" or "return". Reason is required.</summary>
public record DecideChangeRequestCommand(string Decision, string Reason, uint Version);
