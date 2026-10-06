-- SPWPOS (SWP391) logical database - generated from the redesigned UML (v2)
-- Portable ANSI-style DDL. Types: CHAR(36)=UUID, TIMESTAMP=UTC instant. Adjust per DBMS:
--   PostgreSQL: UUID / TIMESTAMPTZ     SQL Server: UNIQUEIDENTIFIER / DATETIME2 / BIT     MySQL: BOOLEAN=TINYINT(1)
-- All user-facing date-times are stored in UTC and rendered with user_profiles.timezone.


-- ===== Account =====
CREATE TABLE users (
  user_id CHAR(36),
  email VARCHAR(255) NOT NULL UNIQUE,
  password_hash VARCHAR(255) NOT NULL,
  role VARCHAR(10) NOT NULL DEFAULT 'USER' CHECK (role IN ('USER','ADMIN')),
  account_status VARCHAR(10) NOT NULL DEFAULT 'ACTIVE' CHECK (account_status IN ('ACTIVE','LOCKED')),
  locked_at TIMESTAMP,
  last_login_at TIMESTAMP,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (user_id)
);

CREATE TABLE user_profiles (
  user_id CHAR(36) REFERENCES users(user_id),
  display_name VARCHAR(100) NOT NULL,
  timezone VARCHAR(64) NOT NULL DEFAULT 'UTC',
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (user_id)
);

CREATE TABLE password_reset_tokens (
  token_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  token_hash VARCHAR(255) NOT NULL UNIQUE,
  expires_at TIMESTAMP NOT NULL,
  used_at TIMESTAMP,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (token_id)
);

-- Audit trail for UC70/UC71
CREATE TABLE account_status_logs (
  log_id CHAR(36),
  target_user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  admin_id CHAR(36) NOT NULL REFERENCES users(user_id),
  action VARCHAR(10) NOT NULL CHECK (action IN ('LOCK','UNLOCK')),
  reason VARCHAR(500),
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (log_id)
);


-- ===== Time =====
CREATE TABLE weekly_availability (
  availability_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  day_of_week SMALLINT NOT NULL CHECK (day_of_week BETWEEN 1 AND 7),
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  PRIMARY KEY (availability_id),
  CHECK (start_time < end_time),
  UNIQUE (user_id, day_of_week, start_time)
);

CREATE TABLE availability_exceptions (
  exception_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  exception_date DATE NOT NULL,
  is_all_day BOOLEAN NOT NULL DEFAULT FALSE,
  start_time TIME,
  end_time TIME,
  exception_type VARCHAR(12) NOT NULL CHECK (exception_type IN ('AVAILABLE','UNAVAILABLE')),
  note VARCHAR(255),
  PRIMARY KEY (exception_id),
  CHECK ((is_all_day AND start_time IS NULL AND end_time IS NULL) OR (NOT is_all_day AND start_time IS NOT NULL AND end_time IS NOT NULL AND start_time < end_time))
);

CREATE TABLE fixed_events (
  fixed_event_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  title VARCHAR(200) NOT NULL,
  description VARCHAR(1000),
  start_at TIMESTAMP NOT NULL,
  end_at TIMESTAMP NOT NULL,
  event_type VARCHAR(12) NOT NULL DEFAULT 'OTHER' CHECK (event_type IN ('MEETING','CLASS','APPOINTMENT','OTHER')),
  PRIMARY KEY (fixed_event_id),
  CHECK (start_at < end_at)
);

CREATE TABLE work_preferences (
  user_id CHAR(36) REFERENCES users(user_id),
  min_session_min INT NOT NULL DEFAULT 25,
  max_session_min INT NOT NULL DEFAULT 120,
  preferred_period VARCHAR(10) NOT NULL DEFAULT 'NONE' CHECK (preferred_period IN ('MORNING','AFTERNOON','EVENING','NONE')),
  planning_horizon_days INT NOT NULL DEFAULT 14 CHECK (planning_horizon_days BETWEEN 1 AND 30),
  PRIMARY KEY (user_id),
  CHECK (min_session_min > 0 AND min_session_min <= max_session_min)
);

CREATE TABLE notification_settings (
  user_id CHAR(36) REFERENCES users(user_id),
  in_app_enabled BOOLEAN NOT NULL DEFAULT TRUE,
  deadline_lead_hours INT NOT NULL DEFAULT 24,
  session_reminder_lead_min INT NOT NULL DEFAULT 10,
  workload_warning_level VARCHAR(10) NOT NULL DEFAULT 'HIGH' CHECK (workload_warning_level IN ('MODERATE','HIGH','OVERLOAD')),
  PRIMARY KEY (user_id)
);


-- ===== Task =====
-- Lookup: PROGRAMMING, WRITING, STUDY, MEETING_PREP, ADMIN, OTHER
CREATE TABLE task_types (
  task_type_code VARCHAR(30),
  display_name VARCHAR(60) NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  PRIMARY KEY (task_type_code)
);

CREATE TABLE task_categories (
  category_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  name VARCHAR(80) NOT NULL,
  PRIMARY KEY (category_id),
  UNIQUE (user_id, name)
);

-- estimated_duration_min = RAW user estimate (kept); adjusted_* = bias-adjusted snapshot used by planner
CREATE TABLE tasks (
  task_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  category_id CHAR(36) REFERENCES task_categories(category_id) ON DELETE SET NULL,
  parent_task_id CHAR(36) REFERENCES tasks(task_id) ON DELETE CASCADE,
  task_type_code VARCHAR(30) NOT NULL DEFAULT 'OTHER' REFERENCES task_types(task_type_code),
  name VARCHAR(200) NOT NULL,
  description VARCHAR(2000),
  deadline TIMESTAMP NOT NULL,
  is_hard_deadline BOOLEAN NOT NULL DEFAULT FALSE,
  priority SMALLINT NOT NULL DEFAULT 2 CHECK (priority BETWEEN 1 AND 4),
  status VARCHAR(12) NOT NULL DEFAULT 'TODO' CHECK (status IN ('TODO','IN_PROGRESS','COMPLETED','CANCELLED')),
  progress_percent SMALLINT NOT NULL DEFAULT 0 CHECK (progress_percent BETWEEN 0 AND 100),
  estimated_duration_min INT NOT NULL CHECK (estimated_duration_min > 0),
  adjusted_duration_min INT,
  adjustment_factor DECIMAL(5,3),
  splittable BOOLEAN NOT NULL DEFAULT TRUE,
  min_session_duration_min INT,
  creation_source VARCHAR(16) NOT NULL DEFAULT 'MANUAL' CHECK (creation_source IN ('MANUAL','NATURAL_LANGUAGE','AI_BREAKDOWN')),
  completed_at TIMESTAMP,
  deleted_at TIMESTAMP,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (task_id),
  CHECK (parent_task_id IS NULL OR parent_task_id <> task_id)
);

CREATE TABLE task_dependencies (
  predecessor_task_id CHAR(36) REFERENCES tasks(task_id) ON DELETE CASCADE,
  successor_task_id CHAR(36) REFERENCES tasks(task_id) ON DELETE CASCADE,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (predecessor_task_id, successor_task_id),
  CHECK (predecessor_task_id <> successor_task_id)
);


-- ===== Planning =====
-- A PROPOSAL (UC36/UC45). Becomes real calendar data only when applied (UC38/UC47)
CREATE TABLE work_plans (
  plan_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  previous_plan_id CHAR(36) REFERENCES work_plans(plan_id) ON DELETE SET NULL,
  plan_kind VARCHAR(10) NOT NULL CHECK (plan_kind IN ('INITIAL','RESCHEDULE')),
  trigger_event VARCHAR(20) NOT NULL DEFAULT 'MANUAL' CHECK (trigger_event IN ('MANUAL','NEW_TASK','TASK_CHANGED','EARLY_COMPLETION','SESSION_MISSED','DEADLINE_CHANGED','AVAILABILITY_CHANGED')),
  status VARCHAR(12) NOT NULL DEFAULT 'PROPOSED' CHECK (status IN ('PROPOSED','APPLIED','DISCARDED','SUPERSEDED')),
  horizon_start TIMESTAMP NOT NULL,
  horizon_end TIMESTAMP NOT NULL,
  generated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  applied_at TIMESTAMP,
  PRIMARY KEY (plan_id),
  CHECK (horizon_start < horizon_end)
);

-- One row = one proposed decision; supports review of kept / moved / unschedulable (UC37, UC46)
CREATE TABLE plan_items (
  item_id CHAR(36),
  plan_id CHAR(36) NOT NULL REFERENCES work_plans(plan_id) ON DELETE CASCADE,
  task_id CHAR(36) NOT NULL REFERENCES tasks(task_id) ON DELETE CASCADE,
  previous_session_id CHAR(36),
  action VARCHAR(12) NOT NULL CHECK (action IN ('ADD','KEEP','MOVE','RESIZE','REMOVE','UNSCHEDULED')),
  start_at TIMESTAMP,
  end_at TIMESTAMP,
  planned_duration_min INT,
  PRIMARY KEY (item_id),
  CHECK ((action IN ('REMOVE','UNSCHEDULED') AND start_at IS NULL) OR (action NOT IN ('REMOVE','UNSCHEDULED') AND start_at IS NOT NULL AND end_at IS NOT NULL AND start_at < end_at))
);

-- Structured reason codes for UC48 Explain Scheduling Decision
CREATE TABLE plan_item_reasons (
  reason_id CHAR(36),
  item_id CHAR(36) NOT NULL REFERENCES plan_items(item_id) ON DELETE CASCADE,
  reason_code VARCHAR(40) NOT NULL,
  detail_json TEXT,
  PRIMARY KEY (reason_id)
);

-- Result of UC44 conflict detection, stored with the proposal
CREATE TABLE plan_warnings (
  warning_id CHAR(36),
  plan_id CHAR(36) NOT NULL REFERENCES work_plans(plan_id) ON DELETE CASCADE,
  task_id CHAR(36) REFERENCES tasks(task_id) ON DELETE SET NULL,
  warning_code VARCHAR(30) NOT NULL CHECK (warning_code IN ('OVERLAP','INSUFFICIENT_SLOT','DEADLINE_CONFLICT','DEPENDENCY_CONFLICT','OVERLOAD')),
  detail VARCHAR(500),
  PRIMARY KEY (warning_id)
);

-- APPLIED calendar sessions only. No-overlap per user enforced in service (transaction) / exclusion constraint
CREATE TABLE work_sessions (
  session_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  task_id CHAR(36) NOT NULL REFERENCES tasks(task_id) ON DELETE CASCADE,
  source_item_id CHAR(36) UNIQUE REFERENCES plan_items(item_id) ON DELETE SET NULL,
  start_at TIMESTAMP NOT NULL,
  end_at TIMESTAMP NOT NULL,
  planned_duration_min INT NOT NULL,
  status VARCHAR(12) NOT NULL DEFAULT 'SCHEDULED' CHECK (status IN ('SCHEDULED','IN_PROGRESS','PAUSED','COMPLETED','SKIPPED','MISSED','CANCELLED')),
  is_locked BOOLEAN NOT NULL DEFAULT FALSE,
  skip_reason VARCHAR(255),
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (session_id),
  CHECK (start_at < end_at),
  CHECK (planned_duration_min > 0)
);


-- ===== Execution =====
-- Exactly 0..1 execution per session. timer_* = system value, actual_* = final value (UC56)
CREATE TABLE session_executions (
  execution_id CHAR(36),
  session_id CHAR(36) NOT NULL UNIQUE REFERENCES work_sessions(session_id) ON DELETE CASCADE,
  started_at TIMESTAMP NOT NULL,
  ended_at TIMESTAMP,
  timer_duration_min INT,
  actual_duration_min INT,
  is_user_adjusted BOOLEAN NOT NULL DEFAULT FALSE,
  PRIMARY KEY (execution_id)
);

-- Running intervals between Start/Resume and Pause/Complete; pause time is the gap
CREATE TABLE execution_segments (
  segment_id CHAR(36),
  execution_id CHAR(36) NOT NULL REFERENCES session_executions(execution_id) ON DELETE CASCADE,
  segment_start TIMESTAMP NOT NULL,
  segment_end TIMESTAMP,
  PRIMARY KEY (segment_id),
  CHECK (segment_end IS NULL OR segment_start < segment_end)
);


-- ===== Intelligence =====
-- One row per completed task: raw input for rolling/weighted bias (Section 6 / 34.5)
CREATE TABLE estimation_observations (
  observation_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  task_id CHAR(36) UNIQUE REFERENCES tasks(task_id) ON DELETE SET NULL,
  task_type_code VARCHAR(30) NOT NULL REFERENCES task_types(task_type_code),
  estimated_min INT NOT NULL CHECK (estimated_min > 0),
  actual_min INT NOT NULL CHECK (actual_min > 0),
  observed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (observation_id)
);

CREATE TABLE personal_capacity_profiles (
  user_id CHAR(36) REFERENCES users(user_id),
  global_estimation_bias DECIMAL(5,3) NOT NULL DEFAULT 1.0,
  capacity_factor DECIMAL(4,3) NOT NULL DEFAULT 0.8,
  observation_count INT NOT NULL DEFAULT 0,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (user_id)
);

CREATE TABLE task_type_biases (
  user_id CHAR(36) REFERENCES users(user_id),
  task_type_code VARCHAR(30) REFERENCES task_types(task_type_code),
  bias_factor DECIMAL(5,3) NOT NULL DEFAULT 1.0,
  avg_error_ratio DECIMAL(5,3) NOT NULL DEFAULT 0,
  sample_count INT NOT NULL DEFAULT 0,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (user_id, task_type_code)
);

-- Weekly Capacity Factor = completed planned / available planned time
CREATE TABLE capacity_snapshots (
  snapshot_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  week_start DATE NOT NULL,
  available_min INT NOT NULL,
  completed_planned_min INT NOT NULL,
  capacity_factor DECIMAL(4,3) NOT NULL,
  PRIMARY KEY (snapshot_id),
  UNIQUE (user_id, week_start)
);

-- TASK uses LOW/MEDIUM/HIGH; OVERLOAD uses LOW/MODERATE/HIGH/OVERLOAD
CREATE TABLE risk_assessments (
  assessment_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  task_id CHAR(36) REFERENCES tasks(task_id) ON DELETE CASCADE,
  scope VARCHAR(10) NOT NULL CHECK (scope IN ('TASK','OVERLOAD')),
  risk_level VARCHAR(10) NOT NULL CHECK (risk_level IN ('LOW','MEDIUM','MODERATE','HIGH','OVERLOAD')),
  score DECIMAL(6,3) NOT NULL,
  load_ratio DECIMAL(6,3),
  is_current BOOLEAN NOT NULL DEFAULT TRUE,
  computed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (assessment_id),
  CHECK ((scope = 'TASK' AND task_id IS NOT NULL) OR (scope = 'OVERLOAD' AND task_id IS NULL))
);

-- Stored reason codes required by Section 10 / NFR Explainability
CREATE TABLE risk_reasons (
  reason_id CHAR(36),
  assessment_id CHAR(36) NOT NULL REFERENCES risk_assessments(assessment_id) ON DELETE CASCADE,
  reason_code VARCHAR(40) NOT NULL,
  detail_json TEXT,
  PRIMARY KEY (reason_id)
);


-- ===== Notification =====
-- dedupe_key stops background jobs from sending the same reminder twice
CREATE TABLE notifications (
  notification_id CHAR(36),
  user_id CHAR(36) NOT NULL REFERENCES users(user_id),
  task_id CHAR(36) REFERENCES tasks(task_id) ON DELETE CASCADE,
  session_id CHAR(36) REFERENCES work_sessions(session_id) ON DELETE CASCADE,
  plan_id CHAR(36) REFERENCES work_plans(plan_id) ON DELETE SET NULL,
  notification_type VARCHAR(20) NOT NULL CHECK (notification_type IN ('DEADLINE','SESSION_REMINDER','WORKLOAD_WARNING','SCHEDULE_CHANGE')),
  title VARCHAR(200) NOT NULL,
  body VARCHAR(1000) NOT NULL,
  dedupe_key VARCHAR(120) NOT NULL,
  scheduled_for TIMESTAMP NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  read_at TIMESTAMP,
  PRIMARY KEY (notification_id),
  UNIQUE (user_id, dedupe_key)
);

-- Source for UC72: failed jobs / error count (AI provider failures are logged here too)
CREATE TABLE system_job_runs (
  job_run_id CHAR(36),
  job_type VARCHAR(24) NOT NULL CHECK (job_type IN ('NOTIFICATION_SCAN','PLAN_GENERATION','RISK_RECALC','AI_REQUEST')),
  user_id CHAR(36) REFERENCES users(user_id) ON DELETE SET NULL,
  status VARCHAR(10) NOT NULL CHECK (status IN ('RUNNING','SUCCESS','FAILED')),
  error_message VARCHAR(1000),
  started_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  finished_at TIMESTAMP,
  PRIMARY KEY (job_run_id)
);

-- Circular reference (plan_items <-> work_sessions) is added after both tables exist
ALTER TABLE plan_items ADD CONSTRAINT fk_plan_items_previous_session_id FOREIGN KEY (previous_session_id) REFERENCES work_sessions(session_id) ON DELETE SET NULL;

CREATE INDEX ix_tasks_user_status_deadline ON tasks(user_id, status, deadline);
CREATE INDEX ix_tasks_parent ON tasks(parent_task_id);
CREATE INDEX ix_sessions_user_time ON work_sessions(user_id, start_at, end_at);
CREATE INDEX ix_sessions_task ON work_sessions(task_id);
CREATE INDEX ix_plan_items_plan ON plan_items(plan_id);
CREATE INDEX ix_fixed_events_user_time ON fixed_events(user_id, start_at, end_at);
CREATE INDEX ix_notifications_user_unread ON notifications(user_id, read_at);
CREATE INDEX ix_obs_user_type ON estimation_observations(user_id, task_type_code);
CREATE INDEX ix_risk_current ON risk_assessments(user_id, scope, is_current);
CREATE INDEX ix_jobs_status ON system_job_runs(status, started_at);

-- Seed lookup data
INSERT INTO task_types(task_type_code, display_name) VALUES ('PROGRAMMING','Programming');
INSERT INTO task_types(task_type_code, display_name) VALUES ('WRITING','Writing');
INSERT INTO task_types(task_type_code, display_name) VALUES ('STUDY','Study');
INSERT INTO task_types(task_type_code, display_name) VALUES ('MEETING_PREP','Meeting preparation');
INSERT INTO task_types(task_type_code, display_name) VALUES ('ADMIN','Admin / chores');
INSERT INTO task_types(task_type_code, display_name) VALUES ('OTHER','Other');

-- ===== Rules the database cannot express on its own (enforce in the service layer) =====
-- R1  No two ACTIVE work_sessions (SCHEDULED/IN_PROGRESS/PAUSED) of one user may overlap, and none may overlap a
--     fixed_event: check inside the same transaction that applies a plan (UC38/UC47) or moves/splits a session.
--     PostgreSQL option: EXCLUDE USING gist (user_id WITH =, tstzrange(start_at,end_at) WITH &&) WHERE (status IN (...)).
-- R2  Sessions must lie inside weekly_availability (+/- availability_exceptions) in the user's timezone.
-- R3  task_dependencies: both tasks belong to the same user and the graph stays acyclic (UC23 cycle check).
-- R4  Subtasks are one level deep; a task that has subtasks is not scheduled itself (its leaf subtasks are), so
--     workload is not double-counted. Subtask deadline <= parent deadline.
-- R5  Applying a plan = ONE transaction: copy plan_items -> work_sessions (locked/completed sessions untouched),
--     set work_plans.status=APPLIED, mark the previous applied plan SUPERSEDED, write schedule-change notification.
-- R6  When a task is COMPLETED insert one estimation_observations row, then update task_type_biases /
--     personal_capacity_profiles (rolling/weighted average). Below the minimum sample count keep bias = 1.0 (cold start).
-- R7  Only one risk_assessments row per (user, scope, task) has is_current = TRUE.
-- R8  Tasks are soft-deleted (deleted_at) so history/observations survive; future sessions of a deleted task are cancelled.
-- R9  What-if scenarios and ad-hoc conflict checks are computed on cloned in-memory state and are never persisted.