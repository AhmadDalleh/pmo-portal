using Microsoft.AspNetCore.Identity;

namespace Pmo.Infrastructure.Identity;

/// <summary>
/// Identity user (table "users"). Lives in Infrastructure so Domain stays free of ASP.NET;
/// domain entities refer to users by int id only.
/// </summary>
public class AppUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? JobTitle { get; set; }

    /// <summary>Inactive users cannot sign in.</summary>
    public bool IsActive { get; set; } = true;
}
