namespace Pmo.Domain.Common;

/// <summary>
/// Base for mutable business tables: audit columns (design doc §3) plus the
/// optimistic-concurrency token mapped to PostgreSQL's xmin system column.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedById { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Maps to xmin; PostgreSQL changes it on every UPDATE.</summary>
    public uint Version { get; set; }
}
