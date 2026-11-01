import { expect, it } from "vitest";
import { agentBlockedHint, isAgentProcessingStop } from "../src/agent-stop.js";

it.each(["AGENT_BUDGET_EXCEEDED", "TOOL_BUDGET_EXCEEDED", "AGENT_TIME_LIMIT"])(
  "explains %s as an interrupted process instead of missing free time",
  (errorCode) => {
    const turn = { status: "BLOCKED", errorCode };
    expect(isAgentProcessingStop(turn)).toBe(true);
    expect(agentBlockedHint(turn)).toContain("Tiếp tục xử lý");
    expect(agentBlockedHint(turn)).not.toMatch(
      /thêm giờ rảnh|deadline|Giờ rảnh/,
    );
  },
);

it("only suggests discussing capacity when the checked plan reports missing minutes", () => {
  const turn = {
    status: "BLOCKED",
    plan: { warnings: [{ unscheduledMinutes: 30 }] },
  };
  expect(isAgentProcessingStop(turn)).toBe(false);
  expect(agentBlockedHint(turn)).toContain("còn thiếu thời gian");
});
