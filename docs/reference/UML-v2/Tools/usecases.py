# Single source of truth for the 72 use cases and their relationships (from the report).
SUBSYSTEMS = [
 ("ACC","Account & Profile"),
 ("TASK","Task Management"),
 ("TIME","Time Management"),
 ("PLAN","Planning & Scheduling"),
 ("EXEC","Execution & Feedback"),
 ("INTEL","Intelligence & Personalization"),
 ("NOTI","Notification"),
 ("ADMIN","Administration"),
]
SUBNAME = dict(SUBSYSTEMS)

RAW = """
ACC|1|Register Account
ACC|2|Login
ACC|3|Logout
ACC|4|Request Password Reset
ACC|5|Reset Password
ACC|6|Change Password
ACC|7|View Profile
ACC|8|Update Profile
TASK|9|Create Task
TASK|10|View Task List
TASK|11|View Task Details
TASK|12|Update Task
TASK|13|Delete Task
TASK|14|Search Tasks
TASK|15|Filter Tasks
TASK|16|Change Task Status
TASK|17|Create Task Category
TASK|18|Update Task Category
TASK|19|Delete Task Category
TASK|20|Create Subtask
TASK|21|Update Subtask
TASK|22|Delete Subtask
TASK|23|Add Task Dependency
TASK|24|Remove Task Dependency
TASK|25|Capture Task Using Natural Language
TASK|26|Break Down Complex Task
TIME|27|Set Weekly Availability
TIME|28|Add Availability Exception
TIME|29|Update Availability Exception
TIME|30|Remove Availability Exception
TIME|31|Create Fixed Event
TIME|32|Update Fixed Event
TIME|33|Delete Fixed Event
TIME|34|View Personal Calendar
TIME|35|Set Work Preferences
PLAN|36|Generate Work Plan
PLAN|37|Review Proposed Plan
PLAN|38|Apply Proposed Plan
PLAN|39|Move Work Session
PLAN|40|Change Work Session Duration
PLAN|41|Lock Work Session
PLAN|42|Unlock Work Session
PLAN|43|Split Work Session
PLAN|44|Detect Scheduling Conflict
PLAN|45|Generate Reschedule Proposal
PLAN|46|Review Reschedule Proposal
PLAN|47|Apply Reschedule Proposal
PLAN|48|Explain Scheduling Decision
PLAN|49|Run What-if Scenario
EXEC|50|Start Work Session
EXEC|51|Pause Work Session
EXEC|52|Resume Work Session
EXEC|53|Complete Work Session
EXEC|54|Skip Work Session
EXEC|55|Update Task Progress
EXEC|56|Record Actual Work Duration
EXEC|57|Review Planned vs Actual Performance
INTEL|58|Get Personalized Duration Estimate
INTEL|59|Review Estimation Bias
INTEL|60|Review Effective Capacity
INTEL|61|View Workload Analysis
INTEL|62|View Overload Risk
INTEL|63|View Task Completion Risk
INTEL|64|View Personal Productivity Insights
NOTI|65|Receive Deadline Notification
NOTI|66|Receive Upcoming Work Session Reminder
NOTI|67|Receive Workload Warning
NOTI|68|Receive Schedule Change Notification
ADMIN|69|Search User Accounts
ADMIN|70|Lock User Account
ADMIN|71|Unlock User Account
ADMIN|72|View System Status
"""
UC = {}
for line in RAW.strip().splitlines():
    s,n,name = line.split("|")
    UC[int(n)] = dict(id=int(n), sub=s, name=name)
assert len(UC)==72

# <<include>>: (base, included, note)
INCLUDE = [
 (9,58,""),(36,58,""),(61,58,""),(61,60,""),
 (36,44,""),(39,44,""),(40,44,""),(43,44,""),(45,44,""),(49,44,""),
 (49,61,""),(62,61,""),(63,61,""),
 (53,56,""),
 (64,59,""),(64,60,""),(64,57,""),
]
# <<extend>>: (extension, base, condition)
EXTEND = [
 (4,2,"forgot password"),
 (11,10,"select a task"),(14,10,"enter keyword"),(15,10,"apply filter"),
 (25,9,"input by sentence"),(26,20,"task is large"),
 (38,37,"user accepts"),(47,46,"user accepts"),
 (48,37,"user asks why"),(48,46,"user asks why"),
 (45,9,"new task"),(45,12,"deadline/duration changed"),(45,53,"finished early / over"),
 (45,54,"session skipped or missed"),(45,28,"availability changed"),(45,32,"event changed"),
 (55,53,"user updates progress"),
 (67,62,"risk > threshold"),(67,44,"important conflict"),
 (68,47,"schedule changed"),
 (70,69,"from result"),(71,69,"from result"),
]
USER_UCS = [n for n in UC if UC[n]['sub']!="ADMIN"]
ADMIN_UCS = [69,70,71,72]
AI_UCS = [25,26]
NOTI_SVC_UCS = [65,66,67,68]
