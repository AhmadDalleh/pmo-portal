namespace Pmo.Application.Features.Snapshots;

/// <summary>Writes one progress snapshot per live project for a date (SQL upsert, so running it twice is safe).</summary>
public interface ISnapshotRunner
{
    /// <summary>Returns how many project rows were inserted or refreshed.</summary>
    Task<int> RunAsync(DateOnly date, CancellationToken cancellationToken);
}
