using Bogus;
using Pmo.Domain.Finance;
using Pmo.Domain.Portfolio;

namespace DataGenerator.Generation;

/// <summary>Projects, their budgets and members.</summary>
public static class ProjectSeeder
{
    public static void Seed(SeedData data, Faker faker)
    {
        var contributors = PeopleSeeder.ContributorIds(data);
        var admin = PeopleSeeder.AdminId(data, 1);
        var id = 1;

        foreach (var plan in ProjectPlan.All)
        {
            var owner = PeopleSeeder.PmId(data, plan.OwnerPm);
            var created = SeedClock.Utc(plan.Start.AddDays(-14));
            data.Projects.Add(new Project
            {
                Id = id, Code = plan.Code, Name = plan.Name, Description = faker.Lorem.Sentence(12),
                DepartmentId = plan.DepartmentId, OwnerId = owner, Status = plan.Status,
                StartDate = plan.Start, PlannedEndDate = plan.PlannedEnd, ActualEndDate = plan.ActualEnd,
                CreatedAt = created, CreatedById = owner
            });
            data.Budgets.Add(new ProjectBudget
            {
                Id = id, ProjectId = id, InitialAmount = plan.InitialBudget, Currency = "AED",
                ApprovedById = admin, ApprovedAt = SeedClock.Utc(plan.Start.AddDays(-7))
            });

            AddMember(data, id, owner, MemberRole.Manager, created);
            for (var k = 0; k < 5; k++)
            {
                AddMember(data, id, contributors[((id - 1) * 3 + k) % contributors.Count], MemberRole.Contributor, created);
            }

            id++;
        }

        // pm4 is a plain contributor on PRJ-001: in scope, but cannot manage it (a 403 demo case).
        AddMember(data, 1, PeopleSeeder.PmId(data, 4), MemberRole.Contributor, SeedClock.Utc(new DateOnly(2025, 9, 20)));
    }

    private static void AddMember(SeedData data, int projectId, int userId, string role, DateTimeOffset addedAt) =>
        data.Members.Add(new ProjectMember { ProjectId = projectId, UserId = userId, MemberRole = role, AddedAt = addedAt });

    public static IReadOnlyList<int> ContributorsOf(SeedData data, int projectId) =>
        data.Members.Where(m => m.ProjectId == projectId && m.MemberRole == MemberRole.Contributor)
            .Select(m => m.UserId).ToList();
}
