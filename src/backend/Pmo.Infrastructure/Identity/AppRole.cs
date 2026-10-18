using Microsoft.AspNetCore.Identity;

namespace Pmo.Infrastructure.Identity;

/// <summary>Identity role (table "roles"): Contributor, ProjectManager, Administrator.</summary>
public class AppRole : IdentityRole<int>
{
}
