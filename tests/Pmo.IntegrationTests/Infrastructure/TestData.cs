using Pmo.Domain.Delivery;
using Pmo.Domain.Finance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;
using Pmo.Infrastructure.Persistence;

namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>Creates the minimum valid rows a schema test needs. Unique codes keep tests independent.</summary>
public static class TestData
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    public static string UniqueCode(string prefix) => prefix + Random.Shared.Next(100_000, 999_999);

    public static async Task<Department> AddDepartmentAsync(PmoDbContext db)
    {
        var department = new Department { Code = UniqueCode("D"), Name = UniqueCode("Department ") };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return department;
    }

    public static async Task<AppUser> AddUserAsync(PmoDbContext db, int departmentId)
    {
        var userName = UniqueCode("user");
        var user = new AppUser { UserName = userName, FullName = userName, DepartmentId = departmentId };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Project> AddProjectAsync(PmoDbContext db, int departmentId, int ownerId)
    {
        var project = new Project
        {
            Code = UniqueCode("PRJ-"), Name = "Test project", DepartmentId = departmentId, OwnerId = ownerId,
            Status = ProjectStatus.Active, StartDate = new DateOnly(2026, 1, 1), PlannedEndDate = new DateOnly(2026, 12, 31),
            CreatedAt = Now, CreatedById = ownerId
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public static async Task<Milestone> AddMilestoneAsync(PmoDbContext db, int projectId, int userId)
    {
        var milestone = new Milestone
        {
            ProjectId = projectId, Name = "M1", DueDate = new DateOnly(2026, 6, 30),
            CreatedAt = Now, CreatedById = userId
        };
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync();
        return milestone;
    }

    public static async Task<CostTransaction> AddCostAsync(PmoDbContext db, int projectId, int userId, string status)
    {
        var category = new CostCategory { Code = UniqueCode("C"), Name = UniqueCode("Category ") };
        db.CostCategories.Add(category);
        await db.SaveChangesAsync();

        var posted = status == CostStatus.Posted;
        var cost = new CostTransaction
        {
            ProjectId = projectId, CategoryId = category.Id, TxnDate = new DateOnly(2026, 8, 15), Amount = 1250.50m,
            Description = "Licences", Status = status, IdempotencyKey = Guid.NewGuid(),
            CreatedById = userId, CreatedAt = Now,
            PostedById = posted ? userId : null, PostedAt = posted ? Now : null
        };
        db.CostTransactions.Add(cost);
        await db.SaveChangesAsync();
        return cost;
    }
}
