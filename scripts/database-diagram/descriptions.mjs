// Editorial descriptions checked against ApplicationDbContext and Domain/Entities.
// They explain application intent; SQL catalog metadata remains authoritative for
// columns and constraints, including databases with pending migrations.
export const descriptions = {
  AspNetUsers: "User accounts, sign-in identity, and account security state.",
  AspNetRoles: "Named authorization roles used by the application.",
  AspNetUserRoles: "Membership links between users and authorization roles.",
  AspNetUserClaims: "Identity claims assigned directly to a user.",
  AspNetRoleClaims: "Identity claims granted through an authorization role.",
  AspNetUserLogins: "External login providers associated with a user identity.",
  AspNetUserTokens: "Identity provider tokens associated with a user.",
  EmailOtps:
    "Email one-time codes used by account verification and security flows.",
  RefreshTokens:
    "Refresh-token records supporting authenticated session renewal.",
  PasswordResetTickets: "Tickets used to authorize the password reset flow.",
  PasswordChangeNotifications:
    "Notification records for account password changes.",
  SecurityAuditLogs:
    "Recorded security events for account and authentication auditing.",
  SecurityRateLimits:
    "Counters and time windows used to limit repeated security requests.",
  account_status_logs:
    "Administrative account status changes, including the acting administrator and reason.",
  user_profiles: "User-facing profile information and time-zone preferences.",
  WorkTasks:
    "Work items with deadlines, estimates, priority, progress, and scheduling preferences. Tasks can have a category and a parent task.",
  TaskCategories: "User-owned task categories and their display colors.",
  TaskDependencies:
    "Task prerequisite links connecting a task to its predecessor.",
  task_types: "Task-type definitions and display names used to classify work.",
  WeeklyAvailability:
    "Recurring weekly time windows available for scheduling work.",
  AvailabilityExceptions:
    "Date-specific overrides to the recurring availability schedule.",
  FixedEvents:
    "Fixed calendar commitments that constrain the time available for work.",
  WorkPreferences:
    "User planning limits, preferred work periods, session lengths, breaks, and time zone.",
  PlanProposals:
    "Proposed planning revisions, their time horizon, trigger, and application status.",
  plan_items:
    "Individual task scheduling changes belonging to a plan proposal.",
  plan_item_reasons:
    "Reason codes and supporting details explaining proposed plan items.",
  plan_warnings:
    "Warnings generated for a plan, optionally associated with a task.",
  WorkSessions:
    "Scheduled task work blocks and their execution status and timer totals.",
  session_executions:
    "Execution records for a scheduled session, including measured and adjusted time.",
  execution_segments:
    "Individual start/end timing segments belonging to a session execution.",
  RecoveryCases:
    "Recovery cases used to track interruptions or changes requiring planning recovery.",
  RecoverySettings: "User settings for the planning recovery workflow.",
  estimation_observations:
    "Completed-work observations comparing task estimates with actual effort.",
  personal_capacity_profiles:
    "Per-user learned estimation bias and available-capacity factors.",
  task_type_biases:
    "Per-user estimation adjustments and observation counts for each task type.",
  capacity_snapshots:
    "Weekly observations of available time and completed planned work.",
  risk_assessments:
    "Computed workload or task risk scores, levels, and current-assessment state.",
  risk_reasons:
    "Reason codes and supporting details explaining a risk assessment.",
  InAppNotifications:
    "User notification messages, their related work objects, and delivery/read state.",
  notification_settings:
    "User notification preferences and reminder lead times.",
  system_job_runs:
    "Operational job execution status, duration, and failure details.",
  PlanningNotes:
    "User planning notes that provide context for planning and assistant workflows.",
  AgentConversations:
    "Conversation records grouping interactions with the planning assistant.",
  AgentTurns:
    "Individual assistant interaction turns belonging to a conversation.",
  AiUsageRecords: "Recorded AI usage for application usage tracking.",
};
