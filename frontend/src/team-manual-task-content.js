import { parseTaskNotes } from "./team-task-notes.jsx";

// Team checklists are stored in the requirements section of the task brief.
// Keep the other sections available in the description when editing a task.
export function manualTaskContent(task) {
  const original = task?.description || "";
  const sections = parseTaskNotes(original);
  const requirements = sections.filter((section) => section.title === "Việc cần thực hiện");
  const checklist = task?.checklist?.map((item) => item.text) ?? requirements
    .flatMap((section) => section.lines)
    .map((line) => line.trim().replace(/^(?:[-*•●▪]|\d+[.)])\s+/, "").replace(/^\[[ xX]\]\s*/, ""))
    .filter(Boolean);
  return {
    description: requirements.length ? sections
      .filter((section) => section.title !== "Việc cần thực hiện")
      .map((section) => `${section.explicit ? `## ${section.title}\n` : ""}${section.lines.join("\n").trim()}`)
      .join("\n\n").trim() : original,
    checklist,
    hasChecklist: requirements.length > 0 || checklist.length > 0,
  };
}

export function manualTaskDescription(description, items, hasChecklist) {
  if (!hasChecklist && !items.some((item) => item.text.trim())) return description;
  const text = description.trim();
  const summary = text && !parseTaskNotes(text)[0]?.explicit ? `TÓM TẮT\n${text}` : text;
  // An empty explicit section also lets an editor remove every old item without
  // turning the summary into an implicit checklist again.
  return [summary, `YÊU CẦU CẦN THỰC HIỆN\n${items.map((item) => item.text.trim())
    .filter(Boolean).map((text) => `- ${text}`).join("\n")}`].filter(Boolean).join("\n\n");
}
