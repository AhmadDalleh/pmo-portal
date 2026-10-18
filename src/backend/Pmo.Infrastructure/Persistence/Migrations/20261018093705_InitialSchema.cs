using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pmo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automation_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    messages_processed = table.Column<int>(type: "integer", nullable: false),
                    messages_failed = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_automation_runs", x => x.id);
                    table.CheckConstraint("ck_automation_runs_status", "status IN ('running', 'succeeded', 'partial', 'failed')");
                });

            migrationBuilder.CreateTable(
                name: "cost_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cost_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    dedup_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_messages_status", "status IN ('pending', 'processed', 'failed')");
                });

            migrationBuilder.CreateTable(
                name: "reporting_settings",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(2026, 9, 30))
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reporting_settings", x => x.id);
                    table.CheckConstraint("ck_reporting_settings_single_row", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: false),
                    job_title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    link_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    source_outbox_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_outbox_messages_source_outbox_id",
                        column: x => x.source_outbox_id,
                        principalTable: "outbox_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    department_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.CheckConstraint("ck_projects_completed_has_end_date", "status <> 'completed' OR actual_end_date IS NOT NULL");
                    table.CheckConstraint("ck_projects_dates", "planned_end_date >= start_date");
                    table.CheckConstraint("ck_projects_status", "status IN ('proposed', 'active', 'on_hold', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_projects_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_projects_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_projects_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_projects_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_events_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cost_impact = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    schedule_impact_days = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by_id = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_id = table.Column<int>(type: "integer", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_requests", x => x.id);
                    table.CheckConstraint("ck_change_requests_decision_complete", "status NOT IN ('approved', 'rejected', 'returned') OR (decided_by_id IS NOT NULL AND decided_at IS NOT NULL AND length(btrim(decision_reason)) > 0)");
                    table.CheckConstraint("ck_change_requests_no_self_decision", "decided_by_id IS NULL OR decided_by_id <> requested_by_id");
                    table.CheckConstraint("ck_change_requests_status", "status IN ('draft', 'submitted', 'returned', 'approved', 'rejected', 'withdrawn')");
                    table.CheckConstraint("ck_change_requests_type", "type IN ('scope', 'schedule', 'budget', 'resource')");
                    table.ForeignKey(
                        name: "fk_change_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_requests_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_requests_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_requests_users_requested_by_id",
                        column: x => x.requested_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_requests_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    txn_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    vendor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    reference_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reverses_transaction_id = table.Column<long>(type: "bigint", nullable: true),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    posted_by_id = table.Column<int>(type: "integer", nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cost_transactions", x => x.id);
                    table.CheckConstraint("ck_cost_transactions_amount_sign", "(reverses_transaction_id IS NULL AND amount > 0) OR (reverses_transaction_id IS NOT NULL AND amount < 0)");
                    table.CheckConstraint("ck_cost_transactions_posted_fields", "(status = 'posted') = (posted_by_id IS NOT NULL AND posted_at IS NOT NULL)");
                    table.CheckConstraint("ck_cost_transactions_status", "status IN ('draft', 'posted')");
                    table.ForeignKey(
                        name: "fk_cost_transactions_cost_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "cost_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cost_transactions_cost_transactions_reverses_transaction_id",
                        column: x => x.reverses_transaction_id,
                        principalTable: "cost_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cost_transactions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cost_transactions_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cost_transactions_users_posted_by_id",
                        column: x => x.posted_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_forecasts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    period_month = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    updated_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_forecasts", x => x.id);
                    table.CheckConstraint("ck_financial_forecasts_amount", "amount >= 0");
                    table.CheckConstraint("ck_financial_forecasts_first_of_month", "EXTRACT(day FROM period_month) = 1");
                    table.ForeignKey(
                        name: "fk_financial_forecasts_cost_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "cost_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_financial_forecasts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_financial_forecasts_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestones", x => x.id);
                    table.UniqueConstraint("ak_milestones_id_project_id", x => new { x.id, x.project_id });
                    table.CheckConstraint("ck_milestones_completed_at", "(status = 'completed') = (completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_milestones_status", "status IN ('pending', 'completed')");
                    table.ForeignKey(
                        name: "fk_milestones_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_milestones_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_milestones_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "progress_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    progress_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tasks_total = table.Column<int>(type: "integer", nullable: false),
                    tasks_done = table.Column<int>(type: "integer", nullable: false),
                    actual_spend = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_progress_snapshots_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_budgets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    initial_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "AED"),
                    approved_by_id = table.Column<int>(type: "integer", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_budgets", x => x.id);
                    table.CheckConstraint("ck_project_budgets_currency", "currency = 'AED'");
                    table.CheckConstraint("ck_project_budgets_initial_amount", "initial_amount >= 0");
                    table.ForeignKey(
                        name: "fk_project_budgets_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_budgets_users_approved_by_id",
                        column: x => x.approved_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    member_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_members", x => new { x.project_id, x.user_id });
                    table.CheckConstraint("ck_project_members_role", "member_role IN ('manager', 'contributor')");
                    table.ForeignKey(
                        name: "fk_project_members_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    probability = table.Column<short>(type: "smallint", nullable: false),
                    impact = table.Column<short>(type: "smallint", nullable: false),
                    score = table.Column<short>(type: "smallint", nullable: false, computedColumnSql: "probability * impact", stored: true),
                    mitigation = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risks", x => x.id);
                    table.CheckConstraint("ck_risks_closed_at", "(status = 'closed') = (closed_at IS NOT NULL)");
                    table.CheckConstraint("ck_risks_impact", "impact BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_risks_probability", "probability BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_risks_status", "status IN ('open', 'mitigating', 'closed')");
                    table.CheckConstraint("ck_risks_type", "type IN ('risk', 'issue')");
                    table.ForeignKey(
                        name: "fk_risks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risks_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risks_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_revisions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    change_request_id = table.Column<int>(type: "integer", nullable: true),
                    requested_by_id = table.Column<int>(type: "integer", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_id = table.Column<int>(type: "integer", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_revisions", x => x.id);
                    table.CheckConstraint("ck_budget_revisions_amount", "amount <> 0");
                    table.CheckConstraint("ck_budget_revisions_no_self_decision", "decided_by_id IS NULL OR decided_by_id <> requested_by_id");
                    table.CheckConstraint("ck_budget_revisions_pending_undecided", "(status = 'pending') = (decided_by_id IS NULL)");
                    table.CheckConstraint("ck_budget_revisions_status", "status IN ('pending', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_budget_revisions_change_requests_change_request_id",
                        column: x => x.change_request_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_budget_revisions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_budget_revisions_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_budget_revisions_users_requested_by_id",
                        column: x => x.requested_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_request_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    change_request_id = table.Column<int>(type: "integer", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_request_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_change_request_history_change_requests_change_request_id",
                        column: x => x.change_request_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_request_history_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    milestone_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    assignee_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    weight = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    progress_pct = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                    table.CheckConstraint("ck_tasks_dates", "start_date IS NULL OR due_date >= start_date");
                    table.CheckConstraint("ck_tasks_done_is_100", "status <> 'done' OR progress_pct = 100");
                    table.CheckConstraint("ck_tasks_priority", "priority IN ('low', 'medium', 'high', 'critical')");
                    table.CheckConstraint("ck_tasks_progress_pct", "progress_pct BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_tasks_status", "status IN ('todo', 'in_progress', 'blocked', 'done', 'cancelled')");
                    table.CheckConstraint("ck_tasks_weight", "weight BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_tasks_milestones_milestone_id_project_id",
                        columns: x => new { x.milestone_id, x.project_id },
                        principalTable: "milestones",
                        principalColumns: new[] { "id", "project_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "reporting_settings",
                columns: new[] { "id", "as_of_date" },
                values: new object[] { (short)1, new DateOnly(2026, 9, 30) });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_id",
                table: "audit_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity_type_entity_id",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_occurred_at",
                table: "audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_project_id",
                table: "audit_events",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_revisions_change_request_id",
                table: "budget_revisions",
                column: "change_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_revisions_decided_by_id",
                table: "budget_revisions",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_revisions_project_id_status",
                table: "budget_revisions",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_budget_revisions_requested_by_id",
                table: "budget_revisions",
                column: "requested_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_request_history_actor_id",
                table: "change_request_history",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_request_history_change_request_id_occurred_at",
                table: "change_request_history",
                columns: new[] { "change_request_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_code",
                table: "change_requests",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_created_by_id",
                table: "change_requests",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_decided_by_id",
                table: "change_requests",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_project_id_status",
                table: "change_requests",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_requested_by_id",
                table: "change_requests",
                column: "requested_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_updated_by_id",
                table: "change_requests",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_categories_code",
                table: "cost_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_categories_name",
                table: "cost_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_transactions_category_id",
                table: "cost_transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_transactions_created_by_id",
                table: "cost_transactions",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_transactions_idempotency_key",
                table: "cost_transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_transactions_posted_by_id",
                table: "cost_transactions",
                column: "posted_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_transactions_project_id_status_txn_date",
                table: "cost_transactions",
                columns: new[] { "project_id", "status", "txn_date" })
                .Annotation("Npgsql:IndexInclude", new[] { "amount", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cost_transactions_reverses_transaction_id",
                table: "cost_transactions",
                column: "reverses_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departments_code",
                table: "departments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departments_name",
                table: "departments",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_forecasts_category_id",
                table: "financial_forecasts",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_forecasts_project_id_category_id_period_month",
                table: "financial_forecasts",
                columns: new[] { "project_id", "category_id", "period_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_forecasts_updated_by_id",
                table: "financial_forecasts",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestones_created_by_id",
                table: "milestones",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestones_project_id_due_date",
                table: "milestones",
                columns: new[] { "project_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_milestones_updated_by_id",
                table: "milestones",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_source_outbox_id_user_id",
                table: "notifications",
                columns: new[] { "source_outbox_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_available_at_pending",
                table: "outbox_messages",
                column: "available_at",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_dedup_key",
                table: "outbox_messages",
                column: "dedup_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_progress_snapshots_project_id_snapshot_date",
                table: "progress_snapshots",
                columns: new[] { "project_id", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_approved_by_id",
                table: "project_budgets",
                column: "approved_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_project_id",
                table: "project_budgets",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_members_user_id",
                table: "project_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_code",
                table: "projects",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_created_by_id",
                table: "projects",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_department_id_status",
                table: "projects",
                columns: new[] { "department_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_projects_owner_id",
                table: "projects",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_start_date_planned_end_date",
                table: "projects",
                columns: new[] { "start_date", "planned_end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_projects_updated_by_id",
                table: "projects",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_risks_created_by_id",
                table: "risks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_risks_owner_id",
                table: "risks",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_risks_project_id_status_score",
                table: "risks",
                columns: new[] { "project_id", "status", "score" });

            migrationBuilder.CreateIndex(
                name: "ix_risks_updated_by_id",
                table: "risks",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_assignee_id_status",
                table: "tasks",
                columns: new[] { "assignee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_created_by_id",
                table: "tasks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_due_date_open",
                table: "tasks",
                column: "due_date",
                filter: "status NOT IN ('done', 'cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_milestone_id_project_id",
                table: "tasks",
                columns: new[] { "milestone_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_project_id_status_due_date",
                table: "tasks",
                columns: new[] { "project_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_updated_by_id",
                table: "tasks",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            // ---- Hand-written SQL (design doc §4): triggers EF cannot express. ----

            // Posted cost transactions are immutable. draft -> posted is still allowed,
            // because in that UPDATE the OLD row is a draft. Corrections use a reversal row.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_block_posted_cost_change() RETURNS trigger AS $$
BEGIN
  IF OLD.status = 'posted' THEN
    RAISE EXCEPTION 'Posted cost transaction % is immutable; create a reversal instead', OLD.id
      USING ERRCODE = 'P0001';
  END IF;
  RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_cost_transactions_posted_immutable
BEFORE UPDATE OR DELETE ON cost_transactions
FOR EACH ROW EXECUTE FUNCTION fn_block_posted_cost_change();");

            // audit_events and change_request_history are append-only: any UPDATE or DELETE fails.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_block_append_only_change() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION 'Table % is append-only; % is not allowed', TG_TABLE_NAME, TG_OP
    USING ERRCODE = 'P0001';
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_events_append_only
BEFORE UPDATE OR DELETE ON audit_events
FOR EACH ROW EXECUTE FUNCTION fn_block_append_only_change();

CREATE TRIGGER trg_change_request_history_append_only
BEFORE UPDATE OR DELETE ON change_request_history
FOR EACH ROW EXECUTE FUNCTION fn_block_append_only_change();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the hand-written triggers and functions first (reverse order of Up).
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_change_request_history_append_only ON change_request_history;
DROP TRIGGER IF EXISTS trg_audit_events_append_only ON audit_events;
DROP FUNCTION IF EXISTS fn_block_append_only_change();
DROP TRIGGER IF EXISTS trg_cost_transactions_posted_immutable ON cost_transactions;
DROP FUNCTION IF EXISTS fn_block_posted_cost_change();");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "automation_runs");

            migrationBuilder.DropTable(
                name: "budget_revisions");

            migrationBuilder.DropTable(
                name: "change_request_history");

            migrationBuilder.DropTable(
                name: "cost_transactions");

            migrationBuilder.DropTable(
                name: "financial_forecasts");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "progress_snapshots");

            migrationBuilder.DropTable(
                name: "project_budgets");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "reporting_settings");

            migrationBuilder.DropTable(
                name: "risks");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "change_requests");

            migrationBuilder.DropTable(
                name: "cost_categories");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "departments");
        }
    }
}
