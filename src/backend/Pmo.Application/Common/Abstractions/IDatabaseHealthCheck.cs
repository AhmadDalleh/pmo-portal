namespace Pmo.Application.Common.Abstractions;

/// <summary>Lets the API ask "can we reach the database?" without touching EF directly.</summary>
public interface IDatabaseHealthCheck
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
