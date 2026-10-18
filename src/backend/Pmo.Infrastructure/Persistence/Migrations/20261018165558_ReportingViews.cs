using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pmo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Read-only views for Power BI and reconciliation (database-design.md section 9). They apply the section 6 definitions
    /// with the reporting date taken from reporting_settings (2026-09-30 for the baseline). Nothing here changes a table.
    /// Each fact view is one row per business record and each project-level view aggregates every one-to-many table
    /// in its own CTE first (section 7), so a SUM over a view cannot be inflated by a join.
    /// </summary>
    public partial class ReportingViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // dim_date: calendar for time intelligence in Power BI (one row per day).
            migrationBuilder.Sql("""
                CREATE VIEW dim_date AS
                SELECT d::date AS date_key,
                       EXTRACT(YEAR FROM d)::int AS year,
                       EXTRACT(QUARTER FROM d)::int AS quarter,
                       EXTRACT(MONTH FROM d)::int AS month_number,
                       to_char(d, 'Mon') AS month_name,
                       date_trunc('month', d)::date AS month_start
                FROM generate_series(DATE '2025-01-01', DATE '2027-12-31', INTERVAL '1 day') AS d
                """);

            // dim_project: one row per project with its department and owner names (the filter source for every fact view).
            migrationBuilder.Sql("""
                CREATE VIEW dim_project AS
                SELECT p.id AS project_id, p.code AS project_code, p.name AS project_name, p.status,
                       d.name AS department, u.full_name AS owner_name,
                       p.start_date, p.planned_end_date, p.actual_end_date
                FROM projects p
                JOIN departments d ON d.id = p.department_id
                JOIN users u ON u.id = p.owner_id
                """);

            // vw_project_financials: section 6.1, one row per project.
            migrationBuilder.Sql("""
                CREATE VIEW vw_project_financials AS
                WITH s AS (SELECT as_of_date FROM reporting_settings WHERE id = 1),
                budget AS (
                    SELECT pb.project_id, pb.currency, pb.initial_amount,
                           pb.initial_amount + COALESCE(SUM(br.amount) FILTER (
                               WHERE br.status = 'approved' AND br.effective_date <= s.as_of_date), 0) AS current_budget
                    FROM project_budgets pb
                    CROSS JOIN s
                    LEFT JOIN budget_revisions br ON br.project_id = pb.project_id
                    GROUP BY pb.project_id, pb.currency, pb.initial_amount, s.as_of_date
                ),
                actual AS (
                    SELECT ct.project_id, SUM(ct.amount) AS actual_spend
                    FROM cost_transactions ct CROSS JOIN s
                    WHERE ct.status = 'posted' AND ct.txn_date <= s.as_of_date
                    GROUP BY ct.project_id
                ),
                ftc AS (
                    SELECT f.project_id, SUM(f.amount) AS forecast_to_complete
                    FROM financial_forecasts f CROSS JOIN s
                    WHERE f.period_month > date_trunc('month', s.as_of_date)::date
                    GROUP BY f.project_id
                ),
                m AS (
                    SELECT b.project_id, b.currency, b.initial_amount, b.current_budget,
                           COALESCE(a.actual_spend, 0) AS actual_spend,
                           COALESCE(f.forecast_to_complete, 0) AS forecast_to_complete,
                           (f.project_id IS NULL) AS has_no_forecast
                    FROM budget b
                    LEFT JOIN actual a ON a.project_id = b.project_id
                    LEFT JOIN ftc f ON f.project_id = b.project_id
                )
                SELECT m.project_id, dp.project_code, dp.project_name, dp.department, dp.owner_name, dp.status,
                       m.currency, m.initial_amount, m.current_budget, m.actual_spend,
                       m.current_budget - m.actual_spend AS remaining_budget,
                       m.actual_spend / NULLIF(m.current_budget, 0) AS utilization,
                       m.forecast_to_complete,
                       m.actual_spend + m.forecast_to_complete AS eac,
                       m.current_budget - (m.actual_spend + m.forecast_to_complete) AS projected_variance,
                       m.has_no_forecast,
                       m.actual_spend > m.current_budget AS is_overspent,
                       m.actual_spend / NULLIF(m.current_budget, 0) BETWEEN 0.90 AND 1.00 AS is_near_budget,
                       m.actual_spend + m.forecast_to_complete > m.current_budget AS is_projected_overrun,
                       (SELECT as_of_date FROM s) AS as_of_date
                FROM m
                JOIN dim_project dp ON dp.project_id = m.project_id
                """);

            // vw_task_facts: weight_for_progress and weighted_progress are 0 for cancelled tasks, so
            // SUM(weighted_progress) / SUM(weight_for_progress) is the section 6 progress formula.
            migrationBuilder.Sql("""
                CREATE VIEW vw_task_facts AS
                WITH s AS (SELECT as_of_date FROM reporting_settings WHERE id = 1)
                SELECT t.id AS task_id, t.project_id, dp.project_code, dp.department,
                       t.title, COALESCE(u.full_name, 'Unassigned') AS assignee, t.status, t.priority,
                       t.start_date, t.due_date, t.weight, t.progress_pct,
                       (t.status NOT IN ('done', 'cancelled') AND t.due_date < s.as_of_date) AS is_overdue,
                       (t.status NOT IN ('done', 'cancelled') AND t.due_date = s.as_of_date) AS is_due_today,
                       (t.status = 'blocked') AS is_blocked,
                       CASE WHEN t.status <> 'cancelled' THEN t.weight ELSE 0 END AS weight_for_progress,
                       CASE WHEN t.status <> 'cancelled' THEN t.weight * t.progress_pct ELSE 0 END AS weighted_progress,
                       s.as_of_date
                FROM tasks t
                CROSS JOIN s
                JOIN dim_project dp ON dp.project_id = t.project_id
                LEFT JOIN users u ON u.id = t.assignee_id
                """);

            // vw_milestone_facts: overdue and due-within-14-days flags (section 6).
            migrationBuilder.Sql("""
                CREATE VIEW vw_milestone_facts AS
                WITH s AS (SELECT as_of_date FROM reporting_settings WHERE id = 1)
                SELECT m.id AS milestone_id, m.project_id, dp.project_code, dp.department,
                       m.name, m.status, m.due_date, m.completed_at,
                       (m.status = 'pending' AND m.due_date < s.as_of_date) AS is_overdue,
                       (m.status = 'pending' AND m.due_date BETWEEN s.as_of_date AND s.as_of_date + 14) AS is_due_soon,
                       s.as_of_date
                FROM milestones m
                CROSS JOIN s
                JOIN dim_project dp ON dp.project_id = m.project_id
                """);

            // vw_risk_facts: open means open or mitigating; high exposure means score (probability x impact) of 15 or more.
            migrationBuilder.Sql("""
                CREATE VIEW vw_risk_facts AS
                SELECT r.id AS risk_id, r.project_id, dp.project_code, dp.department,
                       r.type, r.title, u.full_name AS owner_name, r.probability, r.impact, r.score,
                       r.status, r.due_date,
                       (r.status IN ('open', 'mitigating')) AS is_open,
                       (r.score >= 15) AS is_high_exposure,
                       (r.status IN ('open', 'mitigating') AND r.score >= 15) AS is_open_high_exposure
                FROM risks r
                JOIN dim_project dp ON dp.project_id = r.project_id
                JOIN users u ON u.id = r.owner_id
                """);

            // vw_cost_facts: only posted costs on or before the reporting date, so SUM(amount) is the actual-spend KPI.
            // Reversals are posted negative rows and net out.
            migrationBuilder.Sql("""
                CREATE VIEW vw_cost_facts AS
                WITH s AS (SELECT as_of_date FROM reporting_settings WHERE id = 1)
                SELECT ct.id AS cost_id, ct.project_id, dp.project_code, dp.department,
                       cc.code AS category_code, cc.name AS category_name,
                       ct.txn_date, date_trunc('month', ct.txn_date)::date AS month_start,
                       ct.amount, ct.description, ct.vendor_name, ct.reference_no,
                       (ct.reverses_transaction_id IS NOT NULL) AS is_reversal
                FROM cost_transactions ct
                CROSS JOIN s
                JOIN dim_project dp ON dp.project_id = ct.project_id
                JOIN cost_categories cc ON cc.id = ct.category_id
                WHERE ct.status = 'posted' AND ct.txn_date <= s.as_of_date
                """);

            // vw_forecast_facts: is_future marks the months that count towards forecast-to-complete.
            migrationBuilder.Sql("""
                CREATE VIEW vw_forecast_facts AS
                WITH s AS (SELECT as_of_date FROM reporting_settings WHERE id = 1)
                SELECT f.id AS forecast_id, f.project_id, dp.project_code, dp.department,
                       cc.code AS category_code, cc.name AS category_name,
                       f.period_month, f.amount,
                       (f.period_month > date_trunc('month', s.as_of_date)::date) AS is_future
                FROM financial_forecasts f
                CROSS JOIN s
                JOIN dim_project dp ON dp.project_id = f.project_id
                JOIN cost_categories cc ON cc.id = f.category_id
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var view in new[]
            {
                "vw_forecast_facts", "vw_cost_facts", "vw_risk_facts", "vw_milestone_facts", "vw_task_facts",
                "vw_project_financials", "dim_project", "dim_date"
            })
            {
                migrationBuilder.Sql($"DROP VIEW IF EXISTS {view}");
            }
        }
    }
}
