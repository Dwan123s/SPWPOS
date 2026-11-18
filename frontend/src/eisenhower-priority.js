export const eisenhowerLabels = {
  Q1: "Q1 · Làm trước",
  Q2: "Q2 · Chủ động lên lịch",
  Q3: "Q3 · Xử lý / phân công",
  Q4: "Q4 · Có thể để sau",
  Unknown: "Chưa phân loại",
};
export const importanceOptions = [
  ["Unknown", "Chưa xác định"],
  ["Important", "Quan trọng"],
  ["Lower", "Ít quan trọng hơn"],
];
export function eisenhowerPriority(task, now = Date.now()) {
  task ||= {};
  const importance = task.importanceLevel ?? (typeof task.importance === "string" ? task.importance : task.importance?.level)
    ?? task.eisenhower?.importance ?? "Unknown";
  const due = task.deadline ? Date.parse(task.deadline) : NaN;
  const urgency = !Number.isFinite(due) ? "Unknown" : due <= now + 86400000 ? "Urgent" : "NotUrgent";
  const quadrant = !["Important", "Lower"].includes(importance) || urgency === "Unknown" ? "Unknown"
    : importance === "Important" ? urgency === "Urgent" ? "Q1" : "Q2" : urgency === "Urgent" ? "Q3" : "Q4";
  const caption = `${importanceOptions.find(([value]) => value === importance)?.[1] || "Chưa xác định"} · ${urgency === "Urgent" ? "Khẩn cấp" : urgency === "NotUrgent" ? "Chưa khẩn cấp" : "Chưa có deadline"}`;
  return { importance, urgency, quadrant, caption, label: eisenhowerLabels[quadrant], rank: ["Q1", "Q2", "Q3", "Q4", "Unknown"].indexOf(quadrant) };
}
