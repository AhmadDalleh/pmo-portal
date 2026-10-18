namespace Pmo.Domain.Finance;

/// <summary>The approved initial budget; exactly one per project.</summary>
public class ProjectBudget
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public decimal InitialAmount { get; set; }
    public string Currency { get; set; } = "AED";
    public int ApprovedById { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
}
