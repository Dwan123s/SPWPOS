# Table spec: (name, group, [columns], [table-level constraints])
# column: (name, type, flags) ; flags: PK, FK:table.col, UQ, NULL, DEF=..., CK=...
ID="CHAR(36)"
T=[]
def t(name, group, cols, cons=(), note=""):
    T.append(dict(name=name, group=group, cols=cols, cons=list(cons), note=note))

# ---------------- Account ----------------
t("users","Account",[
 ("user_id",ID,"PK"),("email","VARCHAR(255)","UQ"),("password_hash","VARCHAR(255)",""),
 ("role","VARCHAR(10)","DEF='USER' CK=role IN ('USER','ADMIN')"),
 ("account_status","VARCHAR(10)","DEF='ACTIVE' CK=account_status IN ('ACTIVE','LOCKED')"),
 ("locked_at","TIMESTAMP","NULL"),("last_login_at","TIMESTAMP","NULL"),
 ("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP"),("updated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")])
t("user_profiles","Account",[
 ("user_id",ID,"PK FK:users.user_id"),("display_name","VARCHAR(100)",""),
 ("timezone","VARCHAR(64)","DEF='UTC'"),("updated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")])
t("password_reset_tokens","Account",[
 ("token_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),("token_hash","VARCHAR(255)","UQ"),
 ("expires_at","TIMESTAMP",""),("used_at","TIMESTAMP","NULL"),("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")])
t("account_status_logs","Account",[
 ("log_id",ID,"PK"),("target_user_id",ID,"FK:users.user_id"),("admin_id",ID,"FK:users.user_id"),
 ("action","VARCHAR(10)","CK=action IN ('LOCK','UNLOCK')"),("reason","VARCHAR(500)","NULL"),
 ("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")],
 note="Audit trail for UC70/UC71")

# ---------------- Time ----------------
t("weekly_availability","Time",[
 ("availability_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("day_of_week","SMALLINT","CK=day_of_week BETWEEN 1 AND 7"),("start_time","TIME",""),("end_time","TIME","")],
 ["CHECK (start_time < end_time)","UNIQUE (user_id, day_of_week, start_time)"])
t("availability_exceptions","Time",[
 ("exception_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),("exception_date","DATE",""),
 ("is_all_day","BOOLEAN","DEF=FALSE"),("start_time","TIME","NULL"),("end_time","TIME","NULL"),
 ("exception_type","VARCHAR(12)","CK=exception_type IN ('AVAILABLE','UNAVAILABLE')"),("note","VARCHAR(255)","NULL")],
 ["CHECK ((is_all_day AND start_time IS NULL AND end_time IS NULL) OR (NOT is_all_day AND start_time IS NOT NULL AND end_time IS NOT NULL AND start_time < end_time))"])
t("fixed_events","Time",[
 ("fixed_event_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),("title","VARCHAR(200)",""),
 ("description","VARCHAR(1000)","NULL"),("start_at","TIMESTAMP",""),("end_at","TIMESTAMP",""),
 ("event_type","VARCHAR(12)","DEF='OTHER' CK=event_type IN ('MEETING','CLASS','APPOINTMENT','OTHER')")],
 ["CHECK (start_at < end_at)"])
t("work_preferences","Time",[
 ("user_id",ID,"PK FK:users.user_id"),("min_session_min","INT","DEF=25"),("max_session_min","INT","DEF=120"),
 ("preferred_period","VARCHAR(10)","DEF='NONE' CK=preferred_period IN ('MORNING','AFTERNOON','EVENING','NONE')"),
 ("planning_horizon_days","INT","DEF=14 CK=planning_horizon_days BETWEEN 1 AND 30")],
 ["CHECK (min_session_min > 0 AND min_session_min <= max_session_min)"])
t("notification_settings","Time",[
 ("user_id",ID,"PK FK:users.user_id"),("in_app_enabled","BOOLEAN","DEF=TRUE"),
 ("deadline_lead_hours","INT","DEF=24"),("session_reminder_lead_min","INT","DEF=10"),
 ("workload_warning_level","VARCHAR(10)","DEF='HIGH' CK=workload_warning_level IN ('MODERATE','HIGH','OVERLOAD')")])

# ---------------- Task ----------------
t("task_types","Task",[
 ("task_type_code","VARCHAR(30)","PK"),("display_name","VARCHAR(60)",""),("is_active","BOOLEAN","DEF=TRUE")],
 note="Lookup: PROGRAMMING, WRITING, STUDY, MEETING_PREP, ADMIN, OTHER")
t("task_categories","Task",[
 ("category_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),("name","VARCHAR(80)","")],
 ["UNIQUE (user_id, name)"])
t("tasks","Task",[
 ("task_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("category_id",ID,"NULL FK:task_categories.category_id ON DELETE SET NULL"),
 ("parent_task_id",ID,"NULL FK:tasks.task_id ON DELETE CASCADE"),
 ("task_type_code","VARCHAR(30)","DEF='OTHER' FK:task_types.task_type_code"),
 ("name","VARCHAR(200)",""),("description","VARCHAR(2000)","NULL"),
 ("deadline","TIMESTAMP",""),("is_hard_deadline","BOOLEAN","DEF=FALSE"),
 ("priority","SMALLINT","DEF=2 CK=priority BETWEEN 1 AND 4"),
 ("status","VARCHAR(12)","DEF='TODO' CK=status IN ('TODO','IN_PROGRESS','COMPLETED','CANCELLED')"),
 ("progress_percent","SMALLINT","DEF=0 CK=progress_percent BETWEEN 0 AND 100"),
 ("estimated_duration_min","INT","CK=estimated_duration_min > 0"),
 ("adjusted_duration_min","INT","NULL"),("adjustment_factor","DECIMAL(5,3)","NULL"),
 ("splittable","BOOLEAN","DEF=TRUE"),("min_session_duration_min","INT","NULL"),
 ("creation_source","VARCHAR(16)","DEF='MANUAL' CK=creation_source IN ('MANUAL','NATURAL_LANGUAGE','AI_BREAKDOWN')"),
 ("completed_at","TIMESTAMP","NULL"),("deleted_at","TIMESTAMP","NULL"),
 ("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP"),("updated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")],
 ["CHECK (parent_task_id IS NULL OR parent_task_id <> task_id)"],
 note="estimated_duration_min = RAW user estimate (kept); adjusted_* = bias-adjusted snapshot used by planner")
t("task_dependencies","Task",[
 ("predecessor_task_id",ID,"PK FK:tasks.task_id ON DELETE CASCADE"),
 ("successor_task_id",ID,"PK FK:tasks.task_id ON DELETE CASCADE"),
 ("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")],
 ["CHECK (predecessor_task_id <> successor_task_id)"])

# ---------------- Planning ----------------
t("work_plans","Planning",[
 ("plan_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("previous_plan_id",ID,"NULL FK:work_plans.plan_id ON DELETE SET NULL"),
 ("plan_kind","VARCHAR(10)","CK=plan_kind IN ('INITIAL','RESCHEDULE')"),
 ("trigger_event","VARCHAR(20)","DEF='MANUAL' CK=trigger_event IN ('MANUAL','NEW_TASK','TASK_CHANGED','EARLY_COMPLETION','SESSION_MISSED','DEADLINE_CHANGED','AVAILABILITY_CHANGED')"),
 ("status","VARCHAR(12)","DEF='PROPOSED' CK=status IN ('PROPOSED','APPLIED','DISCARDED','SUPERSEDED')"),
 ("horizon_start","TIMESTAMP",""),("horizon_end","TIMESTAMP",""),
 ("generated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP"),("applied_at","TIMESTAMP","NULL")],
 ["CHECK (horizon_start < horizon_end)"],
 note="A PROPOSAL (UC36/UC45). Becomes real calendar data only when applied (UC38/UC47)")
t("plan_items","Planning",[
 ("item_id",ID,"PK"),("plan_id",ID,"FK:work_plans.plan_id ON DELETE CASCADE"),
 ("task_id",ID,"FK:tasks.task_id ON DELETE CASCADE"),
 ("previous_session_id",ID,"NULL FK:work_sessions.session_id ON DELETE SET NULL"),
 ("action","VARCHAR(12)","CK=action IN ('ADD','KEEP','MOVE','RESIZE','REMOVE','UNSCHEDULED')"),
 ("start_at","TIMESTAMP","NULL"),("end_at","TIMESTAMP","NULL"),("planned_duration_min","INT","NULL")],
 ["CHECK ((action IN ('REMOVE','UNSCHEDULED') AND start_at IS NULL) OR (action NOT IN ('REMOVE','UNSCHEDULED') AND start_at IS NOT NULL AND end_at IS NOT NULL AND start_at < end_at))"],
 note="One row = one proposed decision; supports review of kept / moved / unschedulable (UC37, UC46)")
t("plan_item_reasons","Planning",[
 ("reason_id",ID,"PK"),("item_id",ID,"FK:plan_items.item_id ON DELETE CASCADE"),
 ("reason_code","VARCHAR(40)",""),("detail_json","TEXT","NULL")],
 note="Structured reason codes for UC48 Explain Scheduling Decision")
t("plan_warnings","Planning",[
 ("warning_id",ID,"PK"),("plan_id",ID,"FK:work_plans.plan_id ON DELETE CASCADE"),
 ("task_id",ID,"NULL FK:tasks.task_id ON DELETE SET NULL"),
 ("warning_code","VARCHAR(30)","CK=warning_code IN ('OVERLAP','INSUFFICIENT_SLOT','DEADLINE_CONFLICT','DEPENDENCY_CONFLICT','OVERLOAD')"),
 ("detail","VARCHAR(500)","NULL")],
 note="Result of UC44 conflict detection, stored with the proposal")
t("work_sessions","Planning",[
 ("session_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("task_id",ID,"FK:tasks.task_id ON DELETE CASCADE"),
 ("source_item_id",ID,"NULL UQ FK:plan_items.item_id ON DELETE SET NULL"),
 ("start_at","TIMESTAMP",""),("end_at","TIMESTAMP",""),("planned_duration_min","INT",""),
 ("status","VARCHAR(12)","DEF='SCHEDULED' CK=status IN ('SCHEDULED','IN_PROGRESS','PAUSED','COMPLETED','SKIPPED','MISSED','CANCELLED')"),
 ("is_locked","BOOLEAN","DEF=FALSE"),("skip_reason","VARCHAR(255)","NULL"),
 ("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP"),("updated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")],
 ["CHECK (start_at < end_at)","CHECK (planned_duration_min > 0)"],
 note="APPLIED calendar sessions only. No-overlap per user enforced in service (transaction) / exclusion constraint")

# ---------------- Execution ----------------
t("session_executions","Execution",[
 ("execution_id",ID,"PK"),("session_id",ID,"UQ FK:work_sessions.session_id ON DELETE CASCADE"),
 ("started_at","TIMESTAMP",""),("ended_at","TIMESTAMP","NULL"),
 ("timer_duration_min","INT","NULL"),("actual_duration_min","INT","NULL"),
 ("is_user_adjusted","BOOLEAN","DEF=FALSE")],
 note="Exactly 0..1 execution per session. timer_* = system value, actual_* = final value (UC56)")
t("execution_segments","Execution",[
 ("segment_id",ID,"PK"),("execution_id",ID,"FK:session_executions.execution_id ON DELETE CASCADE"),
 ("segment_start","TIMESTAMP",""),("segment_end","TIMESTAMP","NULL")],
 ["CHECK (segment_end IS NULL OR segment_start < segment_end)"],
 note="Running intervals between Start/Resume and Pause/Complete; pause time is the gap")

# ---------------- Personalization ----------------
t("estimation_observations","Intelligence",[
 ("observation_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("task_id",ID,"NULL UQ FK:tasks.task_id ON DELETE SET NULL"),
 ("task_type_code","VARCHAR(30)","FK:task_types.task_type_code"),
 ("estimated_min","INT","CK=estimated_min > 0"),("actual_min","INT","CK=actual_min > 0"),
 ("observed_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")],
 note="One row per completed task: raw input for rolling/weighted bias (Section 6 / 34.5)")
t("personal_capacity_profiles","Intelligence",[
 ("user_id",ID,"PK FK:users.user_id"),("global_estimation_bias","DECIMAL(5,3)","DEF=1.0"),
 ("capacity_factor","DECIMAL(4,3)","DEF=0.8"),("observation_count","INT","DEF=0"),
 ("updated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")])
t("task_type_biases","Intelligence",[
 ("user_id",ID,"PK FK:users.user_id"),("task_type_code","VARCHAR(30)","PK FK:task_types.task_type_code"),
 ("bias_factor","DECIMAL(5,3)","DEF=1.0"),("avg_error_ratio","DECIMAL(5,3)","DEF=0"),
 ("sample_count","INT","DEF=0"),("updated_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")])
t("capacity_snapshots","Intelligence",[
 ("snapshot_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),("week_start","DATE",""),
 ("available_min","INT",""),("completed_planned_min","INT",""),("capacity_factor","DECIMAL(4,3)","")],
 ["UNIQUE (user_id, week_start)"],
 note="Weekly Capacity Factor = completed planned / available planned time")
t("risk_assessments","Intelligence",[
 ("assessment_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("task_id",ID,"NULL FK:tasks.task_id ON DELETE CASCADE"),
 ("scope","VARCHAR(10)","CK=scope IN ('TASK','OVERLOAD')"),
 ("risk_level","VARCHAR(10)","CK=risk_level IN ('LOW','MEDIUM','MODERATE','HIGH','OVERLOAD')"),
 ("score","DECIMAL(6,3)",""),("load_ratio","DECIMAL(6,3)","NULL"),
 ("is_current","BOOLEAN","DEF=TRUE"),("computed_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP")],
 ["CHECK ((scope = 'TASK' AND task_id IS NOT NULL) OR (scope = 'OVERLOAD' AND task_id IS NULL))"],
 note="TASK uses LOW/MEDIUM/HIGH; OVERLOAD uses LOW/MODERATE/HIGH/OVERLOAD")
t("risk_reasons","Intelligence",[
 ("reason_id",ID,"PK"),("assessment_id",ID,"FK:risk_assessments.assessment_id ON DELETE CASCADE"),
 ("reason_code","VARCHAR(40)",""),("detail_json","TEXT","NULL")],
 note="Stored reason codes required by Section 10 / NFR Explainability")

# ---------------- Notification & Ops ----------------
t("notifications","Notification",[
 ("notification_id",ID,"PK"),("user_id",ID,"FK:users.user_id"),
 ("task_id",ID,"NULL FK:tasks.task_id ON DELETE CASCADE"),
 ("session_id",ID,"NULL FK:work_sessions.session_id ON DELETE CASCADE"),
 ("plan_id",ID,"NULL FK:work_plans.plan_id ON DELETE SET NULL"),
 ("notification_type","VARCHAR(20)","CK=notification_type IN ('DEADLINE','SESSION_REMINDER','WORKLOAD_WARNING','SCHEDULE_CHANGE')"),
 ("title","VARCHAR(200)",""),("body","VARCHAR(1000)",""),("dedupe_key","VARCHAR(120)",""),
 ("scheduled_for","TIMESTAMP",""),("created_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP"),("read_at","TIMESTAMP","NULL")],
 ["UNIQUE (user_id, dedupe_key)"],
 note="dedupe_key stops background jobs from sending the same reminder twice")
t("system_job_runs","Notification",[
 ("job_run_id",ID,"PK"),
 ("job_type","VARCHAR(24)","CK=job_type IN ('NOTIFICATION_SCAN','PLAN_GENERATION','RISK_RECALC','AI_REQUEST')"),
 ("user_id",ID,"NULL FK:users.user_id ON DELETE SET NULL"),
 ("status","VARCHAR(10)","CK=status IN ('RUNNING','SUCCESS','FAILED')"),
 ("error_message","VARCHAR(1000)","NULL"),("started_at","TIMESTAMP","DEF=CURRENT_TIMESTAMP"),("finished_at","TIMESTAMP","NULL")],
 note="Source for UC72: failed jobs / error count (AI provider failures are logged here too)")

# Extra indexes
INDEXES=[
 "CREATE INDEX ix_tasks_user_status_deadline ON tasks(user_id, status, deadline)",
 "CREATE INDEX ix_tasks_parent ON tasks(parent_task_id)",
 "CREATE INDEX ix_sessions_user_time ON work_sessions(user_id, start_at, end_at)",
 "CREATE INDEX ix_sessions_task ON work_sessions(task_id)",
 "CREATE INDEX ix_plan_items_plan ON plan_items(plan_id)",
 "CREATE INDEX ix_fixed_events_user_time ON fixed_events(user_id, start_at, end_at)",
 "CREATE INDEX ix_notifications_user_unread ON notifications(user_id, read_at)",
 "CREATE INDEX ix_obs_user_type ON estimation_observations(user_id, task_type_code)",
 "CREATE INDEX ix_risk_current ON risk_assessments(user_id, scope, is_current)",
 "CREATE INDEX ix_jobs_status ON system_job_runs(status, started_at)",
]
SEED_TYPES=[("PROGRAMMING","Programming"),("WRITING","Writing"),("STUDY","Study"),("MEETING_PREP","Meeting preparation"),("ADMIN","Admin / chores"),("OTHER","Other")]

def parse(flags):
    import re
    d=dict(pk=False,uq=False,null=False,fk=None,ondel=None,default=None,check=None)
    f=flags.strip()
    m=re.search(r"\bCK=(.*)$",f)          # CK is always last
    if m: d['check']=m.group(1).strip(); f=f[:m.start()].strip()
    m=re.search(r"\bDEF=(\S+)",f)         # defaults never contain spaces
    if m: d['default']=m.group(1); f=(f[:m.start()]+" "+f[m.end():]).strip()
    m=re.search(r"FK:(\w+)\.(\w+)(?:\s+ON DELETE\s+(.+?))?\s*$",f)
    if m:
        d['fk']=(m.group(1),m.group(2)); d['ondel']=m.group(3); f=f[:m.start()].strip()
    toks=f.split()
    d['pk']="PK" in toks; d['uq']="UQ" in toks; d['null']="NULL" in toks
    return d
