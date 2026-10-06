import usecases as U
from collections import defaultdict

HEAD = """@startuml
left to right direction
skinparam dpi 110
skinparam shadowing false
skinparam defaultFontName Arial
skinparam defaultFontSize 12
skinparam packageStyle rectangle
skinparam usecase {
  BackgroundColor #FFFFFF
  BorderColor #333333
  ArrowColor #333333
}
skinparam rectangle {
  BackgroundColor #FCFCFC
  BorderColor #555555
}
skinparam ArrowFontSize 10
skinparam nodesep 18
skinparam ranksep 45
"""
def uid(n): return "UC%02d"%n
def label(n): return "%s\\n%s"%(uid(n), U.UC[n]['name'].replace(" Using ","\\nUsing ").replace(" Work Session"," Work\\nSession") if len(U.UC[n]['name'])>26 else U.UC[n]['name'])

def uc_decl(n, external=False):
    style = " #E8E8E8;line.dashed;line:666666" if external else ""
    return 'usecase "%s" as %s%s'%(label(n), uid(n), style)

def actor_links(ucs, lines, user=True, admin=True, ai=True, noti=True):
    for n in ucs:
        if n in U.USER_UCS and user: lines.append("User --> %s"%uid(n))
        if n in U.ADMIN_UCS and admin: lines.append("Admin --> %s"%uid(n))
    for n in ucs:
        if n in U.AI_UCS and ai: lines.append("%s -- AI"%uid(n))
        if n in U.NOTI_SVC_UCS and noti: lines.append("%s -- NS"%uid(n))

def rel_lines(shown, lines):
    for b,i,_ in U.INCLUDE:
        if b in shown and i in shown:
            lines.append("%s ..> %s : <<include>>"%(uid(b),uid(i)))
    for e,b,c in U.EXTEND:
        if e in shown and b in shown:
            lines.append('%s ..> %s : <<extend>>\\n{%s}'%(uid(e),uid(b),c))

def legend():
    return """legend bottom
  <<include>> : base use case ALWAYS performs the included use case (arrow points to included).
  <<extend>>  : extending use case is OPTIONAL / conditional (arrow points to base; {condition} shown).
  Solid line : association (actor takes part).  Grey dashed use case : defined in another subsystem.
  Every User use case presupposes a logged-in session (UC02) except UC01, UC02, UC04, UC05; this is a
  precondition, not an <<include>>.
endlegend"""

def subsystem_diagram(code):
    mine = [n for n in U.UC if U.UC[n]['sub']==code]
    shown = set(mine)
    ext = set()
    for b,i,_ in U.INCLUDE:
        if b in shown and i not in shown: ext.add(i)
        if i in shown and b not in shown: ext.add(b)
    for e,b,_ in U.EXTEND:
        if e in shown and b not in shown: ext.add(b)
        if b in shown and e not in shown: ext.add(e)
    allshown = shown|ext
    L=[HEAD, "title SPWPOS - Use Case Diagram: %s (UC%02d-UC%02d)"%(U.SUBNAME[code],min(mine),max(mine))]
    has_user = any(n in U.USER_UCS for n in mine)
    if has_user: L.append('actor "User" as User')
    if any(n in U.ADMIN_UCS for n in mine): L.append('actor "Administrator" as Admin')
    if any(n in U.AI_UCS for n in mine): L.append('actor "External AI Service\\n<<secondary>>" as AI')
    if any(n in U.NOTI_SVC_UCS for n in mine): L.append('actor "Notification Service\\n<<secondary>>" as NS')
    L.append('rectangle "SPWPOS System - %s" {'%U.SUBNAME[code])
    for n in mine: L.append("  "+uc_decl(n))
    L.append("}")
    if ext:
        L.append('rectangle "Referenced from other subsystems" #F4F4F4 {')
        for n in sorted(ext): L.append("  "+uc_decl(n, True))
        L.append("}")
    actor_links(mine, L)
    rel_lines(allshown, L)
    # only draw relations that touch this subsystem
    L.append(legend())
    L.append("@enduml")
    out="\n".join(L)
    return out

def complete_diagram():
    L=[HEAD.replace("left to right direction","left to right direction"), "title SPWPOS - Complete Use Case Diagram (UC01-UC72)"]
    L.append('actor "User" as User')
    L.append('actor "Administrator" as Admin')
    L.append('actor "External AI Service\\n<<secondary>>" as AI')
    L.append('actor "Notification Service\\n<<secondary>>" as NS')
    L.append('rectangle "SPWPOS System" {')
    for code,name in U.SUBSYSTEMS:
        L.append('  rectangle "%s" {'%name)
        for n in U.UC:
            if U.UC[n]['sub']==code: L.append("    "+uc_decl(n))
        L.append("  }")
    L.append("}")
    actor_links(list(U.UC), L)
    rel_lines(set(U.UC), L)
    L.append(legend()); L.append("@enduml")
    return "\n".join(L)

def overview_diagram():
    cnt = defaultdict(int)
    rng = {}
    for n,u in U.UC.items():
        cnt[u['sub']]+=1
    for code,_ in U.SUBSYSTEMS:
        ids=[n for n in U.UC if U.UC[n]['sub']==code]; rng[code]=(min(ids),max(ids))
    L=[HEAD.replace("left to right direction","left to right direction"),
       "title SPWPOS - Use Case Overview (system context, 72 use cases in 8 subsystems)"]
    L.append('actor "User" as User')
    L.append('actor "Administrator" as Admin')
    L.append('actor "External AI Service\\n<<secondary>>" as AI')
    L.append('actor "Notification Service\\n<<secondary>>" as NS')
    L.append('rectangle "SPWPOS System" {')
    for code,name in U.SUBSYSTEMS:
        a,b=rng[code]
        L.append('  rectangle "**%s**\\nUC%02d-UC%02d (%d use cases)" as P_%s #FFFFFF'%(name,a,b,cnt[code],code))
    L.append("}")
    for code in ["ACC","TASK","TIME","PLAN","EXEC","INTEL","NOTI"]:
        L.append("User --> P_%s"%code)
    L.append("Admin --> P_ADMIN")
    L.append("P_TASK -- AI : UC25, UC26")
    L.append("P_NOTI -- NS : UC65-UC68")
    agg=defaultdict(list)
    for b,i,_ in U.INCLUDE:
        s1,s2=U.UC[b]['sub'],U.UC[i]['sub']
        if s1!=s2: agg[(s1,s2,"include")].append("UC%02d->UC%02d"%(b,i))
    for e,b,_ in U.EXTEND:
        s1,s2=U.UC[e]['sub'],U.UC[b]['sub']
        if s1!=s2: agg[(s1,s2,"extend")].append("UC%02d->UC%02d"%(e,b))
    for (s1,s2,k),v in sorted(agg.items()):
        # include: base package -> included package ; extend: extension package -> base package
        L.append('P_%s ..> P_%s : <<%s>>\\n%s'%(s1,s2,k,", ".join(v)))
    L.append("""legend bottom
  Dependency arrows summarise cross-subsystem <<include>> / <<extend>> relationships between individual use cases
  (full detail in the eight subsystem diagrams and the complete diagram).
endlegend""")
    L.append("@enduml")
    return "\n".join(L)

if __name__=="__main__":
    import os
    d="out/src"; os.makedirs(d,exist_ok=True)
    open(d+"/SPWPOS_UseCase_00_Overview.puml","w").write(overview_diagram())
    for i,(code,name) in enumerate(U.SUBSYSTEMS,1):
        open(d+"/SPWPOS_UseCase_%02d_%s.puml"%(i,code),"w").write(subsystem_diagram(code))
