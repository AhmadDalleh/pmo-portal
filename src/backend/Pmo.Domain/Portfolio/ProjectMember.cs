namespace Pmo.Domain.Portfolio;

/// <summary>Who can see/work on a project. Composite PK (ProjectId, UserId).</summary>
public class ProjectMember
{
    public int ProjectId { get; set; }
    public int UserId { get; set; }
    public string MemberRole { get; set; } = Portfolio.MemberRole.Contributor;
    public DateTimeOffset AddedAt { get; set; }
}
