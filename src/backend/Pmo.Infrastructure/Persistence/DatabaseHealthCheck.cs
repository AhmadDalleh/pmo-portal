using Pmo.Application.Common.Abstractions;

namespace Pmo.Infrastructure.Persistence;

public class DatabaseHealthCheck : IDatabaseHealthCheck
{
    private readonly PmoDbContext _db;

    public DatabaseHealthCheck(PmoDbContext db)
    {
        _db = db;
    }

    /// <summary>Opens a connection; returns false (does not throw) when PostgreSQL is unreachable.</summary>
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return _db.Database.CanConnectAsync(cancellationToken);
    }
}
