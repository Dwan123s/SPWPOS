import dbspec as D

def col_sql(c, inline_fk=True, skip_fk=()):
    name,typ,flags=c; p=D.parse(flags)
    parts=[name,typ]
    if not p['null'] and not p['pk']: parts.append("NOT NULL")
    if p['default'] is not None: parts.append("DEFAULT "+p['default'])
    if p['uq'] and not any(x in flags.split() for x in ["PK"]): parts.append("UNIQUE")
    if p['check']: parts.append("CHECK (%s)"%p['check'])
    if p['fk'] and inline_fk and (name,) not in skip_fk:
        r="REFERENCES %s(%s)"%p['fk']
        if p['ondel']: r+=" ON DELETE "+p['ondel']
        parts.append(r)
    return "  "+" ".join(parts)

CIRCULAR=[("plan_items","previous_session_id","work_sessions","session_id","SET NULL")]

def table_sql(t, inline_circular):
    skip=[]
    if not inline_circular:
        skip=[(c[1],) for c in CIRCULAR if c[0]==t['name']]
    lines=[col_sql(c, True, skip) for c in t['cols']]
    pks=[c[0] for c in t['cols'] if D.parse(c[2])['pk']]
    if pks: lines.append("  PRIMARY KEY (%s)"%", ".join(pks))
    for con in t['cons']: lines.append("  "+con)
    comment = ("-- "+t['note']+"\n") if t['note'] else ""
    return comment+"CREATE TABLE %s (\n%s\n);"%(t['name'], ",\n".join(lines))

def build(inline_circular=False):
    out=["-- SPWPOS (SWP391) logical database - generated from the redesigned UML (v2)",
         "-- Portable ANSI-style DDL. Types: CHAR(36)=UUID, TIMESTAMP=UTC instant. Adjust per DBMS:",
         "--   PostgreSQL: UUID / TIMESTAMPTZ     SQL Server: UNIQUEIDENTIFIER / DATETIME2 / BIT     MySQL: BOOLEAN=TINYINT(1)",
         "-- All user-facing date-times are stored in UTC and rendered with user_profiles.timezone.",""]
    last=None
    for t in D.T:
        if t['group']!=last:
            out.append("\n-- ===== %s ====="%t['group']); last=t['group']
        out.append(table_sql(t, inline_circular)); out.append("")
    if not inline_circular:
        for (tb,col,rt,rc,od) in CIRCULAR:
            out.append("-- Circular reference (plan_items <-> work_sessions) is added after both tables exist")
            out.append("ALTER TABLE %s ADD CONSTRAINT fk_%s_%s FOREIGN KEY (%s) REFERENCES %s(%s) ON DELETE %s;"%(tb,tb,col,col,rt,rc,od))
    out.append("")
    out.extend(x+";" for x in D.INDEXES)
    out.append("")
    out.append("-- Seed lookup data")
    for c,n in D.SEED_TYPES:
        out.append("INSERT INTO task_types(task_type_code, display_name) VALUES ('%s','%s');"%(c,n))
    out.append("")
    out.append("""-- ===== Rules the database cannot express on its own (enforce in the service layer) =====
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
-- R9  What-if scenarios and ad-hoc conflict checks are computed on cloned in-memory state and are never persisted.""")
    return "\n".join(out)

if __name__=="__main__":
    open("out/SPWPOS_Database_Schema.sql","w").write(build(False))
    open("sqlite_test.sql","w").write(build(True))
    print("ok")
