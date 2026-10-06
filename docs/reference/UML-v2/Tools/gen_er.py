import dbspec as D
SHORT={"CHAR(36)":"UUID"}
def ty(t): return SHORT.get(t,t)
HEAD="""@startuml
hide circle
skinparam dpi 110
skinparam shadowing false
skinparam defaultFontName Arial
skinparam defaultFontSize 11
skinparam linetype ortho
skinparam packageStyle rectangle
skinparam nodesep 40
skinparam ranksep 55
skinparam entity {
  BackgroundColor #FFFFFF
  BorderColor #333333
  HeaderBackgroundColor #E6EEF8
}
skinparam package {
  BorderColor #888888
  BackgroundColor #FAFAFA
  FontStyle bold
}
skinparam ArrowColor #444444
skinparam ArrowFontSize 9
"""
COLORS={"Account":"#F3F3FB","Time":"#F1FAF1","Task":"#FFF8E8","Planning":"#FDF0F0","Execution":"#FDF0F0","Intelligence":"#EEF8FA","Notification":"#F7F2FA"}
GROUPNAME={"Account":"Account","Time":"Time & Settings","Task":"Task","Planning":"Planning (proposal -> applied)","Execution":"Execution","Intelligence":"Intelligence & Personalization","Notification":"Notification & Operations"}

def entity(t):
    pks=[c for c in t['cols'] if D.parse(c[2])['pk']]
    rest=[c for c in t['cols'] if not D.parse(c[2])['pk']]
    def line(c):
        p=D.parse(c[2]); tags=[]
        if p['pk']: tags.append("PK")
        if p['fk']: tags.append("FK")
        if p['uq']: tags.append("UQ")
        star="* " if (not p['null']) else ""
        tg=(" <<%s>>"%",".join(tags)) if tags else ""
        return "    %s%s : %s%s"%(star,c[0],ty(c[1]),tg)
    out=['  entity "%s" as %s {'%(t['name'],t['name'])]
    out+= [line(c) for c in pks]
    if pks and rest: out.append("    --")
    out+= [line(c) for c in rest]
    out.append("  }")
    return "\n".join(out)

def rels():
    L=[]
    for t in D.T:
        pkcols=[c[0] for c in t['cols'] if D.parse(c[2])['pk']]
        for c in t['cols']:
            p=D.parse(c[2])
            if not p['fk']: continue
            parent=p['fk'][0]
            one_to_one = (p['pk'] and len(pkcols)==1) or p['uq']
            parent_end = "|o" if p['null'] else "||"
            child_end  = ("o|" if p['null'] or not (p['pk'] and len(pkcols)==1) else "||") if one_to_one else "o{"
            if one_to_one and p['pk']: child_end="o|" if t['name'] in ("work_preferences","notification_settings","personal_capacity_profiles") else "||"
            if one_to_one and not p['pk']: child_end="o|"
            lab=c[0]
            if t['name']==parent: lab=c[0]  # self reference
            L.append('%s %s--%s %s : %s'%(parent,parent_end,child_end,t['name'],lab))
    return L

def build():
    L=[HEAD,"title SPWPOS - Database ER Diagram v2 (crow's foot: || exactly one, |o zero or one, o{ zero or many; * = NOT NULL)"]
    groups={}
    for t in D.T: groups.setdefault(t['group'],[]).append(t)
    for g,ts in groups.items():
        L.append('package "%s" %s {'%(GROUPNAME[g],COLORS[g]))
        for t in ts: L.append(entity(t))
        L.append("}")
    L+=rels()
    L.append("""legend bottom
  PK primary key, FK foreign key, UQ unique.  work_plans/plan_items are PROPOSALS; work_sessions holds only APPLIED calendar sessions.
  Not persisted by design: what-if scenarios, ad-hoc conflict checks, AI suggestions before the user confirms.
endlegend""")
    L.append("@enduml")
    return "\n".join(L)
if __name__=="__main__":
    open("out/src/SPWPOS_Database_ER_v2.puml","w").write(build())
