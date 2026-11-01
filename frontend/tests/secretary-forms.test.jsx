import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { App } from "../src/App.jsx";
import { timePage } from "../src/page-time.jsx";
import { scenarioPage } from "../src/page-scenario.jsx";
import { NotesPage } from "../src/notes.jsx";
import { openTask, captureModal, openDetail } from "../src/tasks.jsx";
import { eventModal, exceptionModal, sessionDetail } from "../src/calendar.jsx";
import { takeAgentDraft } from "../src/agent-handoff.js";
import { state } from "../src/ui.jsx";
import { update } from "../src/store.js";
import { workspace, resetFixtures } from "./fixtures.js";

beforeEach(() => {
  resetFixtures();
  state.workspace = workspace();
  state.workspace.features = [
    ...(state.workspace.features || []),
    "secretary-v1",
  ];
  takeAgentDraft();
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    toasts: [],
    slots: {},
  });
  window.history.replaceState(null, "", "/");
});

it("shows the real timetable and confirmed hours without availability or preference forms", () => {
  render(timePage());
  expect(
    screen.getByRole("heading", { name: "Thời gian biểu của bạn" }),
  ).toBeTruthy();
  expect(document.querySelector("input[type='number'], input[type='time'], select, form[data-form='availability']")).toBeNull();
  fireEvent.click(
    screen.getByRole("button", { name: "Báo thay đổi với NORA" }),
  );
  expect(location.hash).toBe("#agent");
  expect(takeAgentDraft()).toContain("thời gian biểu");
});

it("routes new and edited work, events and exceptions to chat instead of parameter forms", () => {
  for (const action of [
    () => openTask(),
    () => openTask(state.workspace.tasks[0]),
    () => captureModal(),
    () => eventModal(),
    () => eventModal(state.workspace.events[0]),
    () => exceptionModal(),
  ]) {
    action();
    expect(location.hash).toBe("#agent");
    expect(takeAgentDraft().length).toBeGreaterThan(10);
    expect(
      document.querySelector(
        "form[data-form='task'], form[data-form='event'], form[data-form='exception']",
      ),
    ).toBeNull();
  }
});

it("lets users finish work without entering percentages or dependencies", () => {
  render(<App initialize={false} />);
  act(() => openDetail(state.workspace.tasks[0].id));
  expect(
    document.querySelector(
      "form[data-form='task-progress'], form[data-form='dependency']",
    ),
  ).toBeNull();
  expect(
    screen.getByRole("button", { name: "Đã hoàn thành công việc" }),
  ).toBeTruthy();
});

it("keeps session tracking while removing manual time and duration editors", () => {
  render(<App initialize={false} />);
  act(() => sessionDetail(state.workspace.sessions[0].id));
  expect(
    document.querySelector(
      "form[data-form='session-edit'], form[data-form='session-split'], form[data-form='actual-duration']",
    ),
  ).toBeNull();
});

it("replaces the scenario parameter form with an unsent conversation request", () => {
  render(scenarioPage());
  expect(document.querySelector("form, input, select, textarea")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Nhờ NORA đánh giá" }));
  expect(takeAgentDraft()).toContain("Chỉ tư vấn");
});

it("keeps the original Notes editor available in secretary mode", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async (url) =>
        new Response(
          JSON.stringify({
            data:
              url === "/api/notes"
                ? []
                : { aiAvailable: true, provider: "Gemini" },
          }),
          { status: 200 },
        ),
    ),
  );
  render(<NotesPage />);
  await screen.findByLabelText("Nội dung ghi chú");
  expect(screen.getByRole("button", { name: "Lưu bản nháp" })).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  ).toBeTruthy();
});
