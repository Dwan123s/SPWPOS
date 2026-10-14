const date = (day, hour = 17, minute = 0) => {
  const d = new Date();
  d.setUTCHours(0, 0, 0, 0);
  d.setUTCDate(d.getUTCDate() + day);
  d.setUTCHours(hour - 7, minute);
  return d.toISOString();
};
const categories = [
  { id: "product", name: "Dự án cá nhân", color: "#80a88d" },
  { id: "study", name: "Học tập", color: "#a9ad7f" },
  { id: "life", name: "Cuộc sống", color: "#8fabc0" },
];
const names = [
  "Hoàn thiện giao diện dự án PLANORA",
  "Chuẩn bị nội dung buổi thảo luận",
  "Đọc tài liệu kiến trúc hệ thống",
  "Viết báo cáo tiến độ tuần",
  "Ôn tập và ghi chú chương 4",
  "Sắp xếp kế hoạch tuần tới",
];
let tasks = names.map((name, index) => ({
  id: `task-${index}`,
  name,
  description: "Công việc minh họa dùng để kiểm tra giao diện.",
  status: index === 0 ? "InProgress" : index === 5 ? "Completed" : "Todo",
  deadline: date(index + 1),
  estimatedMinutes: 90 + index * 30,
  adjustedMinutes: 100 + index * 30,
  remainingMinutes: index === 5 ? 0 : 80 + index * 20,
  actualMinutes: index === 0 ? 30 : index === 5 ? 120 : 0,
  plannedMinutes: 60,
  progress: index === 0 ? 35 : index === 5 ? 100 : 0,
  priority: index === 0 ? 4 : index === 1 ? 3 : 2,
  categoryId: categories[index % 3].id,
  parentId: null,
  dependencies: [],
  taskType: [
    "PROGRAMMING",
    "MEETING_PREP",
    "STUDY",
    "WRITING",
    "STUDY",
    "OTHER",
  ][index],
  isHardDeadline: true,
  splittable: true,
  minimumSessionMinutes: 25,
  creationSource: "MANUAL",
}));
let sessions = Array.from({ length: 7 }, (_, index) => ({
  id: `session-${index}`,
  taskId: tasks[index % 5].id,
  taskName: tasks[index % 5].name,
  start: date(index < 3 ? 0 : index - 2, index < 3 ? 9 + index * 2 : 10),
  end: date(index < 3 ? 0 : index - 2, index < 3 ? 10 + index * 2 : 11),
  status: index === 0 ? "Completed" : "Scheduled",
  isLocked: index === 3,
  reasonCode: "DEADLINE_PRIORITY_CAPACITY",
  actualMinutes: index === 0 ? 55 : 0,
  actualSeconds: index === 0 ? 3300 : 0,
}));
const profile = {
  id: "preview-user",
  firstName: "Minh",
  lastName: "Nguyễn",
  displayName: "Minh Nguyễn",
  email: "minh@example.test",
  avatarUrl: null,
  bio: "Dành thời gian cho những điều quan trọng.",
};
const workspace = () => ({
  profile,
  tasks,
  sessions,
  categories,
  availability: [1, 2, 3, 4, 5].map((dayOfWeek) => ({
    dayOfWeek,
    startMinute: 480,
    endMinute: 1080,
  })),
  exceptions: [],
  events: [
    {
      id: "event-1",
      name: "Thảo luận nhóm dự án",
      start: date(1, 14),
      end: date(1, 15),
      eventType: "MEETING",
      description: "",
    },
  ],
  preference: {
    timeZoneId: "Asia/Ho_Chi_Minh",
    minimumSessionMinutes: 25,
    maximumSessionMinutes: 90,
    breakMinutes: 15,
    planningHorizonDays: 14,
    preferredPeriod: "MORNING",
    preferredStartMinute: 480,
    preferredEndMinute: 1080,
  },
  notifications: [
    {
      id: "notification-1",
      kind: "DEADLINE",
      title: "Một deadline đang đến gần",
      message: "Chuẩn bị nội dung buổi thảo luận đến hạn trong 48 giờ.",
      createdAt: date(0, 8),
      isRead: false,
      taskId: "task-1",
    },
    {
      id: "notification-2",
      kind: "SCHEDULE_CHANGE",
      title: "Kế hoạch đã được cập nhật",
      message: "Các phiên đã khóa được giữ nguyên khi lập lại lịch.",
      createdAt: date(-1),
      isRead: true,
    },
  ],
  notificationSettings: {
    inAppEnabled: true,
    deadlineLeadHours: 24,
    sessionReminderLeadMinutes: 10,
    workloadWarningLevel: "HIGH",
  },
});
const insights = {
  remainingMinutes: 720,
  effectiveCapacityMinutes: 1440,
  calendarFreeMinutes: 1800,
  horizonDemandMinutes: 720,
  capacityFactor: 0.8,
  capacitySamples: 8,
  riskLevel: "Moderate",
  loadRatio: 0.5,
  plannedMinutes: 480,
  actualMinutes: 525,
  biases: [
    { taskType: "All", samples: 6, personalized: true, ratio: 1.09 },
    { taskType: "PROGRAMMING", samples: 3, personalized: true, ratio: 1.18 },
    { taskType: "STUDY", samples: 2, personalized: false, ratio: 1 },
  ],
  reasons: [],
  tasks: [],
};
const plan = {
  id: "preview-plan",
  start: date(0, 8),
  end: date(7, 17),
  sessions: sessions.slice(1),
  warnings: [],
  explanations: [
    "Xếp phiên trong giờ rảnh và chừa thời gian nghỉ.",
    "Giữ nguyên phiên đã khóa.",
  ],
  status: "READY",
  isApplied: false,
};

const fixtureBaseline = JSON.stringify({ tasks, sessions, profile });
export function resetFixtures() {
  const initial = JSON.parse(fixtureBaseline);
  tasks = initial.tasks;
  sessions = initial.sessions;
  Object.keys(profile).forEach((key) => delete profile[key]);
  Object.assign(profile, initial.profile);
  plan.sessions = sessions.slice(1);
}
export { workspace, insights, plan, profile };
