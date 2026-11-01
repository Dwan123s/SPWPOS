import { expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import {
  needsTimetableSetup,
  nextWorkflowStep,
  workflowSteps,
} from "../src/workflow.js";
import { NoraResultCard } from "../src/nora-result-card.jsx";
import { state } from "../src/ui.jsx";

const empty = () => ({
  events: [],
  availability: [],
  exceptions: [],
  tasks: [],
  sessions: [],
});

it("starts a new workspace with the fixed timetable before work hours or tasks", () => {
  const workspace = empty();
  expect(needsTimetableSetup(workspace)).toBe(true);
  expect(nextWorkflowStep(workspace).title).toBe("Gửi thời gian biểu");
  workspace.events.push({ eventType: "CLASS" });
  expect(needsTimetableSetup(workspace)).toBe(false);
  expect(nextWorkflowStep(workspace).title).toBe(
    "Xác nhận giờ có thể làm việc",
  );
  workspace.availability.push({
    dayOfWeek: 1,
    startMinute: 1140,
    endMinute: 1320,
  });
  expect(nextWorkflowStep(workspace).title).toBe("Giao việc cho NORA");
  workspace.tasks.push({ status: "Todo" });
  expect(nextWorkflowStep(workspace).route).toBe("agent");
  expect(workflowSteps(workspace)[3].title).toBe("Sắp xếp công việc");
});

it("keeps existing user data accessible and does not generate any starter data", () => {
  const workspace = empty();
  workspace.tasks.push({ status: "Todo" });
  const before = JSON.stringify(workspace);
  expect(needsTimetableSetup(workspace)).toBe(false);
  workflowSteps(workspace);
  expect(JSON.stringify(workspace)).toBe(before);
});

it("shows the saved timetable analysis directly so the next setup question is visible", () => {
  state.workspace = empty();
  render(
    <NoraResultCard
      turn={{
        status: "APPLIED",
        canUndo: true,
        changes: [],
        reply:
          "Đã lưu thời khóa biểu. Giờ rảnh: chưa xác nhận. Bạn có thể làm việc vào khung giờ nào?",
      }}
      onAction={vi.fn()}
      onViewResult={vi.fn()}
    />,
  );
  const report = screen.getByText(/Giờ rảnh: chưa xác nhận/);
  expect(report.closest("details")).toBeNull();
  expect(screen.getByRole("button", { name: "Hoàn tác" })).toBeTruthy();
});
