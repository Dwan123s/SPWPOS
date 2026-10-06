using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignWithUmlV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdjustedMinutes",
                table: "WorkTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AdjustmentFactor",
                table: "WorkTasks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationSource",
                table: "WorkTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "WorkTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHardDeadline",
                table: "WorkTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "WorkSessions",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "WorkSessions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceItemId",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "WorkSessions",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "PlanningHorizonDays",
                table: "WorkPreferences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PreferredPeriod",
                table: "WorkPreferences",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "TaskDependencies",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAt",
                table: "PlanProposals",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HorizonEnd",
                table: "PlanProposals",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HorizonStart",
                table: "PlanProposals",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "PlanProposals",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousPlanId",
                table: "PlanProposals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PlanProposals",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TriggerEvent",
                table: "PlanProposals",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "InAppNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledFor",
                table: "InAppNotifications",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "InAppNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "FixedEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "FixedEvents",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "account_status_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_status_logs", x => x.Id);
                    table.UniqueConstraint("AK_account_status_logs_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_account_status_logs_AspNetUsers_AdminId",
                        column: x => x.AdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_status_logs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capacity_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailableMinutes = table.Column<int>(type: "int", nullable: false),
                    CompletedPlannedMinutes = table.Column<int>(type: "int", nullable: false),
                    CapacityFactor = table.Column<double>(type: "float", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capacity_snapshots", x => x.Id);
                    table.UniqueConstraint("AK_capacity_snapshots_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_capacity_snapshots_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DeadlineLeadHours = table.Column<int>(type: "int", nullable: false),
                    SessionReminderLeadMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkloadWarningLevel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_notification_settings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personal_capacity_profiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GlobalEstimationBias = table.Column<double>(type: "float", nullable: false),
                    CapacityFactor = table.Column<double>(type: "float", nullable: false),
                    ObservationCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_capacity_profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_personal_capacity_profiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    End = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedMinutes = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_items", x => x.Id);
                    table.UniqueConstraint("AK_plan_items_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_plan_items_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_items_PlanProposals_UserId_PlanId",
                        columns: x => new { x.UserId, x.PlanId },
                        principalTable: "PlanProposals",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_items_WorkSessions_UserId_PreviousSessionId",
                        columns: x => new { x.UserId, x.PreviousSessionId },
                        principalTable: "WorkSessions",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_items_WorkTasks_UserId_TaskId",
                        columns: x => new { x.UserId, x.TaskId },
                        principalTable: "WorkTasks",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_warnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_warnings", x => x.Id);
                    table.UniqueConstraint("AK_plan_warnings_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_plan_warnings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_warnings_PlanProposals_UserId_PlanId",
                        columns: x => new { x.UserId, x.PlanId },
                        principalTable: "PlanProposals",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_warnings_WorkTasks_UserId_TaskId",
                        columns: x => new { x.UserId, x.TaskId },
                        principalTable: "WorkTasks",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    LoadRatio = table.Column<double>(type: "float", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_assessments", x => x.Id);
                    table.UniqueConstraint("AK_risk_assessments_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_risk_assessments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_risk_assessments_WorkTasks_UserId_TaskId",
                        columns: x => new { x.UserId, x.TaskId },
                        principalTable: "WorkTasks",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TimerSeconds = table.Column<long>(type: "bigint", nullable: false),
                    ActualSeconds = table.Column<long>(type: "bigint", nullable: false),
                    IsUserAdjusted = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_executions", x => x.Id);
                    table.UniqueConstraint("AK_session_executions_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_session_executions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_executions_WorkSessions_UserId_SessionId",
                        columns: x => new { x.UserId, x.SessionId },
                        principalTable: "WorkSessions",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_job_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_job_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_job_runs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_types",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_types", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_user_profiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_item_reasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_item_reasons", x => x.Id);
                    table.UniqueConstraint("AK_plan_item_reasons_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_plan_item_reasons_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_item_reasons_plan_items_UserId_ItemId",
                        columns: x => new { x.UserId, x.ItemId },
                        principalTable: "plan_items",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_reasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_reasons", x => x.Id);
                    table.UniqueConstraint("AK_risk_reasons_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_risk_reasons_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_risk_reasons_risk_assessments_UserId_AssessmentId",
                        columns: x => new { x.UserId, x.AssessmentId },
                        principalTable: "risk_assessments",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "execution_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_segments", x => x.Id);
                    table.UniqueConstraint("AK_execution_segments_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_execution_segments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_execution_segments_session_executions_UserId_ExecutionId",
                        columns: x => new { x.UserId, x.ExecutionId },
                        principalTable: "session_executions",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estimation_observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    ActualMinutes = table.Column<double>(type: "float", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estimation_observations", x => x.Id);
                    table.UniqueConstraint("AK_estimation_observations_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_estimation_observations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_estimation_observations_WorkTasks_UserId_TaskId",
                        columns: x => new { x.UserId, x.TaskId },
                        principalTable: "WorkTasks",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_estimation_observations_task_types_TaskType",
                        column: x => x.TaskType,
                        principalTable: "task_types",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_type_biases",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BiasFactor = table.Column<double>(type: "float", nullable: false),
                    AverageErrorRatio = table.Column<double>(type: "float", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_type_biases", x => new { x.UserId, x.TaskType });
                    table.ForeignKey(
                        name: "FK_task_type_biases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_type_biases_task_types_TaskType",
                        column: x => x.TaskType,
                        principalTable: "task_types",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "task_types",
                columns: new[] { "Code", "DisplayName", "IsActive" },
                values: new object[,]
                {
                    { "ADMIN", "Admin / chores", true },
                    { "MEETING_PREP", "Meeting preparation", true },
                    { "OTHER", "Other", true },
                    { "PROGRAMMING", "Programming", true },
                    { "STUDY", "Study", true },
                    { "WRITING", "Writing", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TaskType",
                table: "WorkTasks",
                column: "TaskType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_SourceItemId",
                table: "WorkSessions",
                column: "SourceItemId",
                unique: true,
                filter: "[SourceItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_UserId_SourceItemId",
                table: "WorkSessions",
                columns: new[] { "UserId", "SourceItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanProposals_UserId_PreviousPlanId",
                table: "PlanProposals",
                columns: new[] { "UserId", "PreviousPlanId" });

            migrationBuilder.Sql("""
                UPDATE WorkTasks SET TaskType = CASE UPPER(TaskType)
                  WHEN 'GENERAL' THEN 'OTHER' WHEN 'LEARNING' THEN 'STUDY' WHEN 'RESEARCH' THEN 'STUDY'
                  WHEN 'PERSONAL' THEN 'ADMIN' WHEN 'MEETING' THEN 'MEETING_PREP' ELSE UPPER(TaskType) END,
                  CreationSource='MANUAL', IsHardDeadline=1, DeletedAt=CASE WHEN IsDeleted=1 THEN UpdatedAt ELSE NULL END;
                INSERT INTO task_types(Code, DisplayName, IsActive)
                  SELECT DISTINCT TaskType, LEFT(TaskType,60), 1 FROM WorkTasks t WHERE NOT EXISTS (SELECT 1 FROM task_types d WHERE d.Code=t.TaskType);
                UPDATE WorkPreferences SET PlanningHorizonDays=14, PreferredPeriod='NONE';
                UPDATE FixedEvents SET EventType='OTHER';
                UPDATE TaskDependencies SET CreatedAt=SYSUTCDATETIME();
                UPDATE WorkSessions SET CreatedAt=Start, UpdatedAt=COALESCE(CompletedAt,Start);
                UPDATE InAppNotifications SET ScheduledFor=CreatedAt, Kind=CASE WHEN Kind='SCHEDULE_CHANGED' THEN 'SCHEDULE_CHANGE' ELSE Kind END;
                INSERT INTO user_profiles(UserId,DisplayName,TimeZoneId,UpdatedAt)
                  SELECT u.Id,LEFT(LTRIM(RTRIM(CONCAT(u.FirstName,' ',u.LastName))),100),COALESCE(p.TimeZoneId,'Asia/Ho_Chi_Minh'),u.UpdatedAt
                  FROM AspNetUsers u LEFT JOIN WorkPreferences p ON p.UserId=u.Id;
                INSERT INTO notification_settings(UserId,InAppEnabled,DeadlineLeadHours,SessionReminderLeadMinutes,WorkloadWarningLevel)
                  SELECT Id,1,24,10,'HIGH' FROM AspNetUsers;
                UPDATE PlanProposals SET Kind='INITIAL', TriggerEvent='MANUAL', Status=CASE WHEN IsApplied=1 THEN 'SUPERSEDED' ELSE 'PROPOSED' END,
                  AppliedAt=CASE WHEN IsApplied=1 THEN CreatedAt ELSE NULL END,
                  HorizonStart=COALESCE(TRY_CONVERT(datetimeoffset,JSON_VALUE(PayloadJson,'$.Start')),CreatedAt),
                  HorizonEnd=COALESCE(TRY_CONVERT(datetimeoffset,JSON_VALUE(PayloadJson,'$.End')),DATEADD(day,7,CreatedAt));
                WITH ranked AS (SELECT Id, ROW_NUMBER() OVER(PARTITION BY UserId ORDER BY CreatedAt DESC, Id DESC) n FROM PlanProposals WHERE IsApplied=1)
                  UPDATE p SET Status='APPLIED' FROM PlanProposals p JOIN ranked r ON r.Id=p.Id WHERE r.n=1;
                INSERT INTO plan_items(Id,UserId,PlanId,TaskId,PreviousSessionId,Action,Start,[End],PlannedMinutes)
                  SELECT NEWID(),p.UserId,p.Id,j.TaskId,CASE WHEN j.Preserved=1 THEN s.Id ELSE NULL END,
                    CASE WHEN j.Preserved=1 THEN 'KEEP' ELSE 'ADD' END,j.Start,j.[End],DATEDIFF(minute,j.Start,j.[End])
                  FROM PlanProposals p CROSS APPLY OPENJSON(p.PayloadJson,'$.Sessions') WITH
                    (Id uniqueidentifier,TaskId uniqueidentifier,Start datetimeoffset,[End] datetimeoffset,Preserved bit) j
                  JOIN WorkTasks t ON t.Id=j.TaskId AND t.UserId=p.UserId LEFT JOIN WorkSessions s ON s.Id=j.Id AND s.UserId=p.UserId;
                INSERT INTO plan_item_reasons(Id,UserId,ItemId,Code,DetailJson)
                  SELECT NEWID(),UserId,Id,'LEGACY_PLAN_IMPORT',NULL FROM plan_items;
                INSERT INTO plan_warnings(Id,UserId,PlanId,TaskId,Code,Detail)
                  SELECT NEWID(),p.UserId,p.Id,j.TaskId,j.Code,j.Message FROM PlanProposals p
                  CROSS APPLY OPENJSON(p.PayloadJson,'$.Warnings') WITH(TaskId uniqueidentifier,Code nvarchar(80),Message nvarchar(1000)) j
                  JOIN WorkTasks t ON t.Id=j.TaskId AND t.UserId=p.UserId;
                WITH links AS
                  (SELECT s.Id SessionId,chosen.Id ItemId,ROW_NUMBER() OVER(PARTITION BY chosen.Id ORDER BY s.Id) n FROM WorkSessions s CROSS APPLY
                    (SELECT TOP(1) i.Id FROM plan_items i JOIN PlanProposals p ON p.Id=i.PlanId
                      WHERE i.UserId=s.UserId AND i.TaskId=s.TaskId AND i.Start=s.Start AND i.[End]=s.[End] AND i.Action='ADD' AND p.IsApplied=1 ORDER BY p.CreatedAt) chosen WHERE s.Status<>6)
                  UPDATE s SET SourceItemId=l.ItemId FROM WorkSessions s JOIN links l ON l.SessionId=s.Id WHERE l.n=1;
                INSERT INTO session_executions(Id,UserId,SessionId,StartedAt,EndedAt,TimerSeconds,ActualSeconds,IsUserAdjusted)
                  SELECT NEWID(),UserId,Id,Start,CASE WHEN Status IN (3,4) THEN COALESCE(CompletedAt,[End]) ELSE NULL END,ActualSeconds,ActualSeconds,0
                  FROM WorkSessions WHERE Status IN (1,2,3) OR ActualSeconds>0;
                INSERT INTO execution_segments(Id,UserId,ExecutionId,Start,[End])
                  SELECT NEWID(),s.UserId,e.Id,s.SegmentStartedAt,NULL FROM WorkSessions s JOIN session_executions e ON e.SessionId=s.Id WHERE s.SegmentStartedAt IS NOT NULL;
                INSERT INTO estimation_observations(Id,UserId,TaskId,TaskType,EstimatedMinutes,ActualMinutes,ObservedAt)
                  SELECT NEWID(),t.UserId,t.Id,t.TaskType,t.EstimatedMinutes,SUM(CONVERT(float,s.ActualSeconds))/60,COALESCE(t.CompletedAt,t.UpdatedAt)
                  FROM WorkTasks t JOIN WorkSessions s ON s.TaskId=t.Id AND s.Status=3 WHERE t.Status=2 AND t.EstimatedMinutes>0
                    AND NOT EXISTS(SELECT 1 FROM WorkTasks child WHERE child.ParentId=t.Id AND child.IsDeleted=0)
                  GROUP BY t.UserId,t.Id,t.TaskType,t.EstimatedMinutes,t.CompletedAt,t.UpdatedAt HAVING SUM(s.ActualSeconds)>0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InAppNotifications_UserId_PlanId",
                table: "InAppNotifications",
                columns: new[] { "UserId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_InAppNotifications_UserId_SessionId",
                table: "InAppNotifications",
                columns: new[] { "UserId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_InAppNotifications_UserId_TaskId",
                table: "InAppNotifications",
                columns: new[] { "UserId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_account_status_logs_AdminId",
                table: "account_status_logs",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_account_status_logs_UserId",
                table: "account_status_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_capacity_snapshots_UserId",
                table: "capacity_snapshots",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_capacity_snapshots_UserId_WeekStart",
                table: "capacity_snapshots",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_estimation_observations_TaskId",
                table: "estimation_observations",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_estimation_observations_TaskType",
                table: "estimation_observations",
                column: "TaskType");

            migrationBuilder.CreateIndex(
                name: "IX_estimation_observations_UserId",
                table: "estimation_observations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_estimation_observations_UserId_TaskId",
                table: "estimation_observations",
                columns: new[] { "UserId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_estimation_observations_UserId_TaskType",
                table: "estimation_observations",
                columns: new[] { "UserId", "TaskType" });

            migrationBuilder.CreateIndex(
                name: "IX_execution_segments_ExecutionId",
                table: "execution_segments",
                column: "ExecutionId",
                unique: true,
                filter: "[End] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_execution_segments_UserId",
                table: "execution_segments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_segments_UserId_ExecutionId",
                table: "execution_segments",
                columns: new[] { "UserId", "ExecutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_item_reasons_UserId",
                table: "plan_item_reasons",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_item_reasons_UserId_ItemId",
                table: "plan_item_reasons",
                columns: new[] { "UserId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_UserId",
                table: "plan_items",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_UserId_PlanId",
                table: "plan_items",
                columns: new[] { "UserId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_UserId_PreviousSessionId",
                table: "plan_items",
                columns: new[] { "UserId", "PreviousSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_UserId_TaskId",
                table: "plan_items",
                columns: new[] { "UserId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_warnings_UserId",
                table: "plan_warnings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_warnings_UserId_PlanId",
                table: "plan_warnings",
                columns: new[] { "UserId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_warnings_UserId_TaskId",
                table: "plan_warnings",
                columns: new[] { "UserId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_UserId",
                table: "risk_assessments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_UserId_Scope_TaskId",
                table: "risk_assessments",
                columns: new[] { "UserId", "Scope", "TaskId" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_UserId_TaskId",
                table: "risk_assessments",
                columns: new[] { "UserId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_reasons_UserId",
                table: "risk_reasons",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_reasons_UserId_AssessmentId",
                table: "risk_reasons",
                columns: new[] { "UserId", "AssessmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_session_executions_SessionId",
                table: "session_executions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_executions_UserId",
                table: "session_executions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_session_executions_UserId_SessionId",
                table: "session_executions",
                columns: new[] { "UserId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_system_job_runs_Status_StartedAt",
                table: "system_job_runs",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_system_job_runs_UserId",
                table: "system_job_runs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_task_type_biases_TaskType",
                table: "task_type_biases",
                column: "TaskType");

            migrationBuilder.AddForeignKey(
                name: "FK_InAppNotifications_PlanProposals_UserId_PlanId",
                table: "InAppNotifications",
                columns: new[] { "UserId", "PlanId" },
                principalTable: "PlanProposals",
                principalColumns: new[] { "UserId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InAppNotifications_WorkSessions_UserId_SessionId",
                table: "InAppNotifications",
                columns: new[] { "UserId", "SessionId" },
                principalTable: "WorkSessions",
                principalColumns: new[] { "UserId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InAppNotifications_WorkTasks_UserId_TaskId",
                table: "InAppNotifications",
                columns: new[] { "UserId", "TaskId" },
                principalTable: "WorkTasks",
                principalColumns: new[] { "UserId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProposals_PlanProposals_UserId_PreviousPlanId",
                table: "PlanProposals",
                columns: new[] { "UserId", "PreviousPlanId" },
                principalTable: "PlanProposals",
                principalColumns: new[] { "UserId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_plan_items_UserId_SourceItemId",
                table: "WorkSessions",
                columns: new[] { "UserId", "SourceItemId" },
                principalTable: "plan_items",
                principalColumns: new[] { "UserId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_task_types_TaskType",
                table: "WorkTasks",
                column: "TaskType",
                principalTable: "task_types",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InAppNotifications_PlanProposals_UserId_PlanId",
                table: "InAppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_InAppNotifications_WorkSessions_UserId_SessionId",
                table: "InAppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_InAppNotifications_WorkTasks_UserId_TaskId",
                table: "InAppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanProposals_PlanProposals_UserId_PreviousPlanId",
                table: "PlanProposals");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_plan_items_UserId_SourceItemId",
                table: "WorkSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_task_types_TaskType",
                table: "WorkTasks");

            migrationBuilder.DropTable(
                name: "account_status_logs");

            migrationBuilder.DropTable(
                name: "capacity_snapshots");

            migrationBuilder.DropTable(
                name: "estimation_observations");

            migrationBuilder.DropTable(
                name: "execution_segments");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "personal_capacity_profiles");

            migrationBuilder.DropTable(
                name: "plan_item_reasons");

            migrationBuilder.DropTable(
                name: "plan_warnings");

            migrationBuilder.DropTable(
                name: "risk_reasons");

            migrationBuilder.DropTable(
                name: "system_job_runs");

            migrationBuilder.DropTable(
                name: "task_type_biases");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "session_executions");

            migrationBuilder.DropTable(
                name: "plan_items");

            migrationBuilder.DropTable(
                name: "risk_assessments");

            migrationBuilder.DropTable(
                name: "task_types");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TaskType",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_SourceItemId",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_UserId_SourceItemId",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_PlanProposals_UserId_PreviousPlanId",
                table: "PlanProposals");

            migrationBuilder.DropIndex(
                name: "IX_InAppNotifications_UserId_PlanId",
                table: "InAppNotifications");

            migrationBuilder.DropIndex(
                name: "IX_InAppNotifications_UserId_SessionId",
                table: "InAppNotifications");

            migrationBuilder.DropIndex(
                name: "IX_InAppNotifications_UserId_TaskId",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "AdjustedMinutes",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "AdjustmentFactor",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "CreationSource",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "IsHardDeadline",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "SourceItemId",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "PlanningHorizonDays",
                table: "WorkPreferences");

            migrationBuilder.DropColumn(
                name: "PreferredPeriod",
                table: "WorkPreferences");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaskDependencies");

            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "HorizonEnd",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "HorizonStart",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "PreviousPlanId",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "TriggerEvent",
                table: "PlanProposals");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "FixedEvents");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "FixedEvents");
        }
    }
}
