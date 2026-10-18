namespace Pmo.Application.Common.Abstractions;

/// <summary>A user as business code needs to see them. Exactly one role per user.</summary>
public record UserSummary(int Id, string Email, string FullName, int DepartmentId, string? JobTitle, string Role, bool IsActive);
