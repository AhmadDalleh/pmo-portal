namespace Pmo.Application.Features.Projects.Commands;

/// <summary>B5. MemberRole is "manager" or "contributor".</summary>
public record AddMemberCommand(int UserId, string MemberRole);
