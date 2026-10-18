using Pmo.Application.Features.Projects;
using Shouldly;

namespace Pmo.UnitTests.Projects;

/// <summary>The health rule of story B4, one case per branch.</summary>
public class ProjectHealthTests
{
    [Theory]
    [InlineData(true, false, 0, 0.5, 0, "red")]        // overspent
    [InlineData(false, true, 0, 0.5, 0, "red")]        // projected overrun
    [InlineData(false, false, 3, 0.5, 0, "red")]       // three overdue tasks
    [InlineData(false, false, 2, 0.5, 0, "amber")]     // some overdue
    [InlineData(false, false, 0, 0.90, 0, "amber")]    // exactly 90% utilisation
    [InlineData(false, false, 0, 0.5, 1, "amber")]     // open high-exposure risk
    [InlineData(false, false, 0, 0.89, 0, "green")]
    [InlineData(false, false, 0, null, 0, "green")]    // no budget figure and nothing else wrong
    public void Evaluate_ReturnsExpectedBadge(bool overspent, bool overrun, int overdue, double? utilization, int risks, string expected)
    {
        var result = ProjectHealth.Evaluate(overspent, overrun, overdue, (decimal?)utilization, risks);

        result.ShouldBe(expected);
    }
}
