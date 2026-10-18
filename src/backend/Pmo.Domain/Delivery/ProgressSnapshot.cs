namespace Pmo.Domain.Delivery;

/// <summary>Historical, derived: one row per project per day.</summary>
public class ProgressSnapshot
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public decimal ProgressPct { get; set; }
    public int TasksTotal { get; set; }
    public int TasksDone { get; set; }
    public decimal ActualSpend { get; set; }
}
