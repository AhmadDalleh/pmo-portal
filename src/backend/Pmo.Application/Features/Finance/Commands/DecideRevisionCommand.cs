namespace Pmo.Application.Features.Finance.Commands;

/// <summary>F4. Decision is "approved" or "rejected".</summary>
public record DecideRevisionCommand(string Decision, string Reason, uint Version);
