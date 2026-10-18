namespace Pmo.Application.Features.Finance.Commands;

/// <summary>F8. The grid as the user edited it; saved as an upsert in one transaction.</summary>
public record SaveForecastsCommand(List<ForecastItem> Items);
