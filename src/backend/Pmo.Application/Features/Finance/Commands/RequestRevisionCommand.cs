namespace Pmo.Application.Features.Finance.Commands;

/// <summary>F3. Positive = increase, negative = reduction. ChangeRequestId optionally links an approved change request.</summary>
public record RequestRevisionCommand(decimal Amount, string Reason, DateOnly EffectiveDate, int? ChangeRequestId);
