namespace Pmo.Application.Common.Abstractions;

/// <summary>Gives services the current request's correlation id (stamped on history and audit rows).</summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
