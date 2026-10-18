namespace Pmo.Application.Features.Snapshots.Dtos;

public record ProgressSnapshotDto(DateOnly SnapshotDate, decimal ProgressPct, int TasksTotal, int TasksDone, decimal ActualSpend);
