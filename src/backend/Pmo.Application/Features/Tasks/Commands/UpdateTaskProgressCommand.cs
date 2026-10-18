namespace Pmo.Application.Features.Tasks.Commands;

/// <summary>C2. The only fields a Contributor may change on their own task.</summary>
public record UpdateTaskProgressCommand(int ProgressPct, string Status, uint Version);
