namespace Pmo.Application.Features.Auth;

/// <summary>The caller's profile. ManagedProjectIds drives which buttons the UI shows (A2); the server re-checks every action.</summary>
public record MeDto(int Id, string Email, string FullName, string Role, int DepartmentId, int[] ManagedProjectIds);
