using DataGenerator.Generation;

namespace DataGenerator.Csv;

/// <summary>
/// Maps the generated objects to CSV tables, one explicit line per column. The order of the list is the
/// import order: parents before children, so foreign keys hold at every step.
/// </summary>
public static class CsvExporter
{
    private static readonly string[] Audit = ["created_at", "created_by_id", "updated_at", "updated_by_id"];

    public static List<CsvTable> BuildTables(SeedData d)
    {
        var departments = new CsvTable("departments", "id", "code", "name", "is_active");
        d.Departments.ForEach(x => departments.Add(x.Id, x.Code, x.Name, x.IsActive));

        // Passwords are not in the CSV: the importer hashes DEMO_USER_PASSWORD for every row.
        var users = new CsvTable("users", "id", "email", "full_name", "department_id", "job_title", "is_active", "role");
        d.Users.ForEach(x => users.Add(x.Id, x.Email, x.FullName, x.DepartmentId, x.JobTitle, x.IsActive, x.Role));

        var projects = new CsvTable("projects", ["id", "code", "name", "description", "department_id", "owner_id", "status",
            "start_date", "planned_end_date", "actual_end_date", .. Audit]);
        d.Projects.ForEach(x => projects.Add(x.Id, x.Code, x.Name, x.Description, x.DepartmentId, x.OwnerId, x.Status,
            x.StartDate, x.PlannedEndDate, x.ActualEndDate, x.CreatedAt, x.CreatedById, x.UpdatedAt, x.UpdatedById));

        var members = new CsvTable("project_members", "project_id", "user_id", "member_role", "added_at");
        d.Members.ForEach(x => members.Add(x.ProjectId, x.UserId, x.MemberRole, x.AddedAt));

        var budgets = new CsvTable("project_budgets", "id", "project_id", "initial_amount", "currency", "approved_by_id", "approved_at");
        d.Budgets.ForEach(x => budgets.Add(x.Id, x.ProjectId, x.InitialAmount, x.Currency, x.ApprovedById, x.ApprovedAt));

        var milestones = new CsvTable("milestones", ["id", "project_id", "name", "description", "due_date", "status", "completed_at", .. Audit]);
        d.Milestones.ForEach(x => milestones.Add(x.Id, x.ProjectId, x.Name, x.Description, x.DueDate, x.Status, x.CompletedAt,
            x.CreatedAt, x.CreatedById, x.UpdatedAt, x.UpdatedById));

        var tasks = new CsvTable("tasks", ["id", "project_id", "milestone_id", "title", "description", "assignee_id", "status", "priority",
            "start_date", "due_date", "weight", "progress_pct", "completed_at", .. Audit]);
        d.Tasks.ForEach(x => tasks.Add(x.Id, x.ProjectId, x.MilestoneId, x.Title, x.Description, x.AssigneeId, x.Status, x.Priority,
            x.StartDate, x.DueDate, x.Weight, x.ProgressPct, x.CompletedAt, x.CreatedAt, x.CreatedById, x.UpdatedAt, x.UpdatedById));

        var snapshots = new CsvTable("progress_snapshots", "id", "project_id", "snapshot_date", "progress_pct", "tasks_total", "tasks_done", "actual_spend");
        d.Snapshots.ForEach(x => snapshots.Add(x.Id, x.ProjectId, x.SnapshotDate, x.ProgressPct, x.TasksTotal, x.TasksDone, x.ActualSpend));

        // "score" is a generated column in PostgreSQL (probability * impact), so it is not exported.
        var risks = new CsvTable("risks", ["id", "project_id", "type", "title", "description", "owner_id", "probability", "impact",
            "mitigation", "due_date", "status", "closed_at", .. Audit]);
        d.Risks.ForEach(x => risks.Add(x.Id, x.ProjectId, x.Type, x.Title, x.Description, x.OwnerId, x.Probability, x.Impact,
            x.Mitigation, x.DueDate, x.Status, x.ClosedAt, x.CreatedAt, x.CreatedById, x.UpdatedAt, x.UpdatedById));

        var crs = new CsvTable("change_requests", ["id", "code", "project_id", "title", "description", "type", "cost_impact",
            "schedule_impact_days", "status", "requested_by_id", "submitted_at", "decided_by_id", "decided_at", "decision_reason", .. Audit]);
        d.ChangeRequests.ForEach(x => crs.Add(x.Id, x.Code, x.ProjectId, x.Title, x.Description, x.Type, x.CostImpact,
            x.ScheduleImpactDays, x.Status, x.RequestedById, x.SubmittedAt, x.DecidedById, x.DecidedAt, x.DecisionReason,
            x.CreatedAt, x.CreatedById, x.UpdatedAt, x.UpdatedById));

        var history = new CsvTable("change_request_history", "id", "change_request_id", "from_status", "to_status", "actor_id",
            "reason", "occurred_at", "correlation_id");
        d.History.ForEach(x => history.Add(x.Id, x.ChangeRequestId, x.FromStatus, x.ToStatus, x.ActorId, x.Reason, x.OccurredAt, x.CorrelationId));

        var audit = new CsvTable("audit_events", "id", "occurred_at", "actor_id", "project_id", "entity_type", "entity_id", "action",
            "correlation_id", "data");
        d.AuditEvents.ForEach(x => audit.Add(x.Id, x.OccurredAt, x.ActorId, x.ProjectId, x.EntityType, x.EntityId, x.Action, x.CorrelationId, x.Data));

        var categories = new CsvTable("cost_categories", "id", "code", "name", "is_active");
        d.Categories.ForEach(x => categories.Add(x.Id, x.Code, x.Name, x.IsActive));

        var revisions = new CsvTable("budget_revisions", "id", "project_id", "amount", "reason", "effective_date", "status",
            "change_request_id", "requested_by_id", "requested_at", "decided_by_id", "decided_at", "decision_reason");
        d.Revisions.ForEach(x => revisions.Add(x.Id, x.ProjectId, x.Amount, x.Reason, x.EffectiveDate, x.Status, x.ChangeRequestId,
            x.RequestedById, x.RequestedAt, x.DecidedById, x.DecidedAt, x.DecisionReason));

        var costs = new CsvTable("cost_transactions", "id", "project_id", "category_id", "txn_date", "amount", "description", "vendor_name",
            "reference_no", "status", "reverses_transaction_id", "idempotency_key", "created_by_id", "created_at", "posted_by_id", "posted_at");
        d.Costs.ForEach(x => costs.Add(x.Id, x.ProjectId, x.CategoryId, x.TxnDate, x.Amount, x.Description, x.VendorName, x.ReferenceNo,
            x.Status, x.ReversesTransactionId, x.IdempotencyKey, x.CreatedById, x.CreatedAt, x.PostedById, x.PostedAt));

        var forecasts = new CsvTable("financial_forecasts", "id", "project_id", "category_id", "period_month", "amount", "updated_by_id", "updated_at");
        d.Forecasts.ForEach(x => forecasts.Add(x.Id, x.ProjectId, x.CategoryId, x.PeriodMonth, x.Amount, x.UpdatedById, x.UpdatedAt));

        var outbox = new CsvTable("outbox_messages", "id", "type", "payload", "dedup_key", "status", "occurred_at", "available_at",
            "attempts", "processed_at", "last_error");
        d.Outbox.ForEach(x => outbox.Add(x.Id, x.Type, x.Payload, x.DedupKey, x.Status, x.OccurredAt, x.AvailableAt, x.Attempts, x.ProcessedAt, x.LastError));

        var runs = new CsvTable("automation_runs", "id", "job_name", "started_at", "finished_at", "status", "messages_processed",
            "messages_failed", "error", "correlation_id");
        d.Runs.ForEach(x => runs.Add(x.Id, x.JobName, x.StartedAt, x.FinishedAt, x.Status, x.MessagesProcessed, x.MessagesFailed, x.Error, x.CorrelationId));

        var notifications = new CsvTable("notifications", "id", "user_id", "title", "body", "link_url", "source_outbox_id", "created_at", "read_at");
        d.Notifications.ForEach(x => notifications.Add(x.Id, x.UserId, x.Title, x.Body, x.LinkUrl, x.SourceOutboxId, x.CreatedAt, x.ReadAt));

        return
        [
            departments, users, projects, members, budgets, milestones, tasks, snapshots, risks, crs, history, audit,
            categories, revisions, costs, forecasts, outbox, runs, notifications
        ];
    }

    public static void WriteAll(SeedData data, string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var table in BuildTables(data))
        {
            File.WriteAllText(Path.Combine(directory, table.Name + ".csv"), table.ToCsv(), new System.Text.UTF8Encoding(false));
        }
    }
}
