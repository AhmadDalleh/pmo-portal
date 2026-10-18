namespace Pmo.Application.Features.Projects.Dtos;

public record MemberDto(int UserId, string FullName, string Email, string UserRole, string MemberRole, DateTimeOffset AddedAt);
