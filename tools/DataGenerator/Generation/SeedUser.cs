namespace DataGenerator.Generation;

/// <summary>A user before import. The password is not here: the importer sets it from DEMO_USER_PASSWORD.</summary>
public record SeedUser(int Id, string Email, string FullName, int DepartmentId, string JobTitle, string Role)
{
    public bool IsActive => true;
}
