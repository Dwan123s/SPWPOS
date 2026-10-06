import sqlite3, uuid
u=lambda: str(uuid.uuid4())
db=sqlite3.connect(":memory:"); db.execute("PRAGMA foreign_keys=ON")
db.executescript(open("sqlite_test.sql").read())
print("schema loads:", db.execute("select count(*) from sqlite_master where type='table'").fetchone()[0], "tables")

uid=u(); db.execute("insert into users(user_id,email,password_hash) values(?,?,?)",(uid,"a@x.com","h"))
db.execute("insert into user_profiles(user_id,display_name,timezone) values(?,?,?)",(uid,"Demo","Asia/Ho_Chi_Minh"))
db.execute("insert into work_preferences(user_id) values(?)",(uid,))
db.execute("insert into personal_capacity_profiles(user_id) values(?)",(uid,))
# availability Mon 4h, Tue 3h ; fixed event Mon 19-20
db.execute("insert into weekly_availability values(?,?,?,?,?)",(u(),uid,1,"16:00","20:00"))
db.execute("insert into weekly_availability values(?,?,?,?,?)",(u(),uid,2,"17:00","20:00"))
db.execute("insert into fixed_events(fixed_event_id,user_id,title,start_at,end_at,event_type) values(?,?,?,?,?,?)",(u(),uid,"Meeting","2026-10-05 19:00","2026-10-05 20:00","MEETING"))
A,B,C=u(),u(),u()
for tid,name,est,dl,tp in [(A,"Implement API",120,"2026-10-06 23:59","PROGRAMMING"),(B,"Write Report",180,"2026-10-07 23:59","WRITING"),(C,"Prepare Slides",60,"2026-10-07 23:59","OTHER")]:
    db.execute("insert into tasks(task_id,user_id,name,deadline,estimated_duration_min,task_type_code) values(?,?,?,?,?,?)",(tid,uid,name,dl,est,tp))
# dependency B after A
db.execute("insert into task_dependencies(predecessor_task_id,successor_task_id) values(?,?)",(A,B))
# subtask
S=u(); db.execute("insert into tasks(task_id,user_id,parent_task_id,name,deadline,estimated_duration_min) values(?,?,?,?,?,?)",(S,uid,B,"Outline","2026-10-06 12:00",30))

# proposal
P=u(); db.execute("insert into work_plans(plan_id,user_id,plan_kind,status,horizon_start,horizon_end) values(?,?,?,?,?,?)",(P,uid,"INITIAL","PROPOSED","2026-10-05 00:00","2026-10-08 00:00"))
items={}
for task,s,e in [(A,"2026-10-05 16:00","2026-10-05 18:00"),(B,"2026-10-06 17:00","2026-10-06 20:00"),(C,"2026-10-05 18:00","2026-10-05 19:00")]:
    iid=u(); items[task]=iid
    db.execute("insert into plan_items(item_id,plan_id,task_id,action,start_at,end_at,planned_duration_min) values(?,?,?,?,?,?,?)",(iid,P,task,"ADD",s,e,60))
    db.execute("insert into plan_item_reasons(reason_id,item_id,reason_code) values(?,?,?)",(u(),iid,"EARLIEST_DEADLINE_FIRST"))
# apply = copy items -> sessions
sessions={}
for task,iid in items.items():
    r=db.execute("select start_at,end_at,planned_duration_min from plan_items where item_id=?",(iid,)).fetchone()
    sid=u(); sessions[task]=sid
    db.execute("insert into work_sessions(session_id,user_id,task_id,source_item_id,start_at,end_at,planned_duration_min) values(?,?,?,?,?,?,?)",(sid,uid,task)+(iid,)+r)
db.execute("update work_plans set status='APPLIED', applied_at=CURRENT_TIMESTAMP where plan_id=?",(P,))
# execute A with a pause: 60 + 30 + (pause) + 90 = actual 180
ex=u(); sid=sessions[A]
db.execute("insert into session_executions(execution_id,session_id,started_at) values(?,?,?)",(ex,sid,"2026-10-05 16:00"))
db.execute("insert into execution_segments values(?,?,?,?)",(u(),ex,"2026-10-05 16:00","2026-10-05 17:30"))
db.execute("insert into execution_segments values(?,?,?,?)",(u(),ex,"2026-10-05 17:45","2026-10-05 19:15"))
act=db.execute("select sum((julianday(segment_end)-julianday(segment_start))*1440) from execution_segments where execution_id=?",(ex,)).fetchone()[0]
db.execute("update session_executions set ended_at='2026-10-05 19:15', timer_duration_min=?, actual_duration_min=? where execution_id=?",(round(act),round(act),ex))
db.execute("update work_sessions set status='COMPLETED' where session_id=?",(sid,))
db.execute("update tasks set status='COMPLETED', progress_percent=100, completed_at='2026-10-05 19:15' where task_id=?",(A,))
db.execute("insert into estimation_observations(observation_id,user_id,task_id,task_type_code,estimated_min,actual_min) values(?,?,?,?,?,?)",(u(),uid,A,"PROGRAMMING",120,round(act)))
bias=round(act)/120
db.execute("insert into task_type_biases(user_id,task_type_code,bias_factor,sample_count) values(?,?,?,1)",(uid,"PROGRAMMING",bias))
print("actual min:",round(act),"bias:",bias)
# reschedule proposal
P2=u(); db.execute("insert into work_plans(plan_id,user_id,previous_plan_id,plan_kind,trigger_event,status,horizon_start,horizon_end) values(?,?,?,?,?,?,?,?)",(P2,uid,P,"RESCHEDULE","EARLY_COMPLETION","PROPOSED","2026-10-05 19:15","2026-10-08 00:00"))
i2=u(); db.execute("insert into plan_items(item_id,plan_id,task_id,previous_session_id,action,start_at,end_at,planned_duration_min) values(?,?,?,?,?,?,?,?)",(i2,P2,B,sessions[B],"MOVE","2026-10-06 16:00","2026-10-06 19:00",180))
db.execute("insert into plan_items(item_id,plan_id,task_id,action,planned_duration_min) values(?,?,?,?,?)",(u(),P2,C,"UNSCHEDULED",60))
db.execute("insert into plan_warnings(warning_id,plan_id,task_id,warning_code) values(?,?,?,?)",(u(),P2,C,"INSUFFICIENT_SLOT"))
# risk + reasons + notification + job
R=u(); db.execute("insert into risk_assessments(assessment_id,user_id,task_id,scope,risk_level,score) values(?,?,?,?,?,?)",(R,uid,C,"TASK","HIGH",0.82))
db.execute("insert into risk_reasons values(?,?,?,NULL)",(u(),R,"INSUFFICIENT_CAPACITY"))
db.execute("insert into notifications(notification_id,user_id,plan_id,notification_type,title,body,dedupe_key,scheduled_for) values(?,?,?,?,?,?,?,?)",(u(),uid,P2,"SCHEDULE_CHANGE","Schedule changed","1 session moved","chg:"+P2,"2026-10-05 19:20"))
db.execute("insert into system_job_runs(job_run_id,job_type,status,error_message) values(?,?,?,?)",(u(),"AI_REQUEST","FAILED","timeout"))
db.commit(); print("demo scenario inserted OK")

# negative tests: all of these MUST fail
def must_fail(label, sql, args=()):
    try: db.execute(sql,args); print("  !! NOT REJECTED:",label)
    except sqlite3.IntegrityError as e: print("  rejected:",label)
must_fail("self dependency","insert into task_dependencies(predecessor_task_id,successor_task_id) values(?,?)",(A,A))
must_fail("duplicate dependency","insert into task_dependencies(predecessor_task_id,successor_task_id) values(?,?)",(A,B))
must_fail("unknown task_type","insert into tasks(task_id,user_id,name,deadline,estimated_duration_min,task_type_code) values(?,?,?,?,?,?)",(u(),uid,"x","2026-10-09",10,"NOPE"))
must_fail("session end<start","insert into work_sessions(session_id,user_id,task_id,start_at,end_at,planned_duration_min) values(?,?,?,?,?,?)",(u(),uid,B,"2026-10-06 10:00","2026-10-06 09:00",60))
must_fail("2nd execution for same session","insert into session_executions(execution_id,session_id,started_at) values(?,?,?)",(u(),sessions[A],"2026-10-05 20:00"))
must_fail("duplicate dedupe_key","insert into notifications(notification_id,user_id,plan_id,notification_type,title,body,dedupe_key,scheduled_for) values(?,?,?,?,?,?,?,?)",(u(),uid,P2,"SCHEDULE_CHANGE","t","b","chg:"+P2,"2026-10-05 19:20"))
must_fail("TASK risk without task","insert into risk_assessments(assessment_id,user_id,scope,risk_level,score) values(?,?,?,?,?)",(u(),uid,"TASK","LOW",0.1))
must_fail("UNSCHEDULED item with times","insert into plan_items(item_id,plan_id,task_id,action,start_at,end_at) values(?,?,?,?,?,?)",(u(),P2,C,"UNSCHEDULED","2026-10-06 10:00","2026-10-06 11:00"))
must_fail("duplicate category name","insert into task_categories values(?,?,?)",(u(),uid,"Study")) if db.execute("insert into task_categories values(?,?,?)",(u(),uid,"Study")) else None
must_fail("session for missing user","insert into work_sessions(session_id,user_id,task_id,start_at,end_at,planned_duration_min) values(?,?,?,?,?,?)",(u(),u(),B,"2026-10-06 10:00","2026-10-06 11:00",60))
# cascade/soft behaviours
db.execute("delete from task_categories"); 
db.execute("delete from tasks where task_id=?",(C,))
print("hard-deleting task C removed its sessions:", db.execute("select count(*) from work_sessions where task_id=?",(C,)).fetchone()[0]==0)
print("foreign_key_check violations:", db.execute("PRAGMA foreign_key_check").fetchall())
