import { expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { agentFailure } from "../src/agent-failure.js";
import { NoraProgress } from "../src/nora-progress.jsx";

it.each([
  ["INVALID_TASK_TYPE", "validation"],
  ["AGENT_INVALID_INTENT", "validation"],
  ["AGENT_AI_QUOTA_EXCEEDED", "provider"],
  ["AGENT_AI_AUTH_FAILED", "provider"],
  ["AGENT_AI_TIMEOUT", "timeout"],
  ["AGENT_STALE", "stale"],
  ["AGENT_CANCELLED", "cancelled"],
  ["", "transport"],
])(
  "classifies %s without presenting every failure as connectivity",
  (code, category) => {
    expect(agentFailure({ code }).category).toBe(category);
  },
);

it("keeps provisional streamed text unfinished after a validation failure", () => {
  render(
    <NoraProgress
      busy={false}
      pending={{
        text: "Làm slide 90 phút",
        reply: "Tôi đã ghi nhận yêu cầu.",
        phases: [],
        startedAt: Date.now(),
        cancelled: false,
        failure: agentFailure({ code: "INVALID_TASK_TYPE" }),
      }}
    />,
  );
  expect(screen.getByText("Yêu cầu chưa hợp lệ")).toBeTruthy();
  expect(screen.queryByText("Kết nối bị gián đoạn")).toBeNull();
  expect(
    screen.getByText(/Phản hồi trên là bản nháp chưa hoàn tất/).textContent,
  ).toContain("Yêu cầu chưa được lưu");
});
