namespace Pmo.Api.Authorization;

/// <summary>Role policies (layer 1 of authorization). Layer 2 is the project-scope check inside each service.</summary>
public static class Policies
{
    public const string ManageProjects = "ManageProjects";   // Project Manager or Administrator
    public const string CanDecide = "CanDecide";             // approve/reject/return change requests
    public const string CanPostCosts = "CanPostCosts";       // draft, post and reverse costs
    public const string AdminOnly = "AdminOnly";
}
