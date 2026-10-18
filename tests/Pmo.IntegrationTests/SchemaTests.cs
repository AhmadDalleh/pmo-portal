using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pmo.Domain.Delivery;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests;

/// <summary>Proves the database itself enforces the design-doc rules, even if application code is bypassed.</summary>
[Collection(ApiCollection.Name)]
public class SchemaTests
{
    private readonly PmoApiFactory _factory;

    public SchemaTests(PmoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdatePostedCost_IsRejectedByTrigger()
    {
        await using var db = _factory.CreateDbContext();
        var (_, user, project) = await ArrangeProjectAsync(db);
        var cost = await TestData.AddCostAsync(db, project.Id, user.Id, CostStatus.Posted);

        var act = () => db.Database.ExecuteSqlAsync(
            $"UPDATE cost_transactions SET description = 'changed' WHERE id = {cost.Id}");

        var ex = await Should.ThrowAsync<PostgresException>(act);
        ex.SqlState.ShouldBe("P0001");
        ex.MessageText.ShouldContain("immutable");
    }

    [Fact]
    public async Task PostDraftCost_IsAllowedByTrigger()
    {
        await using var db = _factory.CreateDbContext();
        var (_, user, project) = await ArrangeProjectAsync(db);
        var cost = await TestData.AddCostAsync(db, project.Id, user.Id, CostStatus.Draft);

        var rows = await db.Database.ExecuteSqlAsync(
            $"UPDATE cost_transactions SET status = 'posted', posted_by_id = {user.Id}, posted_at = now() WHERE id = {cost.Id}");

        rows.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAuditEvent_IsRejectedByTrigger()
    {
        await using var db = _factory.CreateDbContext();
        var audit = new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow, EntityType = "Test", EntityId = "1", Action = "test.created",
            Data = """{"key": "value"}"""
        };
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();

        var act = () => db.Database.ExecuteSqlAsync($"UPDATE audit_events SET action = 'x' WHERE id = {audit.Id}");

        var ex = await Should.ThrowAsync<PostgresException>(act);
        ex.SqlState.ShouldBe("P0001");
    }

    [Fact]
    public async Task ChangeRequest_SelfDecision_ViolatesCheck()
    {
        await using var db = _factory.CreateDbContext();
        var (_, user, project) = await ArrangeProjectAsync(db);
        db.ChangeRequests.Add(new ChangeRequest
        {
            Code = TestData.UniqueCode("CR-"), ProjectId = project.Id, Title = "Self approved", Description = "x",
            Type = ChangeRequestType.Budget, Status = ChangeRequestStatus.Approved,
            RequestedById = user.Id, DecidedById = user.Id, DecidedAt = DateTimeOffset.UtcNow, DecisionReason = "ok",
            CreatedAt = DateTimeOffset.UtcNow, CreatedById = user.Id
        });

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var pg = ex.InnerException.ShouldBeOfType<PostgresException>();
        pg.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        pg.ConstraintName.ShouldBe("ck_change_requests_no_self_decision");
    }

    [Fact]
    public async Task Task_WithMilestoneFromOtherProject_ViolatesFk()
    {
        await using var db = _factory.CreateDbContext();
        var (department, user, project) = await ArrangeProjectAsync(db);
        var otherProject = await TestData.AddProjectAsync(db, department.Id, user.Id);
        var otherMilestone = await TestData.AddMilestoneAsync(db, otherProject.Id, user.Id);
        db.Tasks.Add(new ProjectTask
        {
            ProjectId = project.Id, MilestoneId = otherMilestone.Id, Title = "Wrong milestone",
            DueDate = new DateOnly(2026, 6, 1), CreatedAt = DateTimeOffset.UtcNow, CreatedById = user.Id
        });

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var pg = ex.InnerException.ShouldBeOfType<PostgresException>();
        pg.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        pg.ConstraintName.ShouldBe("fk_tasks_milestones_milestone_id_project_id");
    }

    private static async Task<(Pmo.Domain.Portfolio.Department, Pmo.Infrastructure.Identity.AppUser, Pmo.Domain.Portfolio.Project)>
        ArrangeProjectAsync(Pmo.Infrastructure.Persistence.PmoDbContext db)
    {
        var department = await TestData.AddDepartmentAsync(db);
        var user = await TestData.AddUserAsync(db, department.Id);
        var project = await TestData.AddProjectAsync(db, department.Id, user.Id);
        return (department, user, project);
    }
}
