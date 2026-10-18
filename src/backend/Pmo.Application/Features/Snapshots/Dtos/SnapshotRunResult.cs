namespace Pmo.Application.Features.Snapshots.Dtos;

public record SnapshotRunResult(DateOnly SnapshotDate, int ProjectsSnapshotted);
