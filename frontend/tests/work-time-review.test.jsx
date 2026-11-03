import { beforeEach, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import {
  WorkTimeReviewCard,
  FinishWorkButton,
} from "../src/work-time-review.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const review = {
  sessionId: "source",
  taskId: "task",
  taskName: "Viết báo cáo",
  start: "2026-11-02T03:00:00Z",
  end: "2026-11-02T05:00:00Z",
  actualSeconds: 7200,
};
const preview = {
  id: "extra",
  revision: "r",
  minutes: 30,
  feasible: true,
  message: "Chỉ thêm giờ khi xác nhận",
  slots: [
    { id: "new", start: "2026-11-02T05:05:00Z", end: "2026-11-02T05:35:00Z" },
  ],
};
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
  state.workspace.features = ["ambient-tracking"];
});
it("asks after the session ends and only saves extra time after a separate confirmation", async () => {
  let answered = false;
  const changed = vi.fn();
  api.mockImplementation(async (url, method) =>
    url.endsWith("/time-review")
      ? answered
        ? []
        : [review]
      : url.endsWith("/preview")
        ? preview
        : ((answered = true), { message: "Đã thêm 30 phút" }),
  );
  render(<WorkTimeReviewCard onWorkspaceChanged={changed} />);
  await screen.findByText("Bạn đã làm xong chưa?");
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "+30 phút" }));
  fireEvent.click(
    screen.getByRole("button", { name: "NORA đề xuất thêm giờ" }),
  );
  await screen.findByRole("button", { name: "Xác nhận thêm 30 phút" });
  expect(api).toHaveBeenCalledWith(
    "/api/sessions/source/extra-time/preview",
    "POST",
    { minutes: 30 },
  );
  expect(api.mock.calls.some(([url]) => url.endsWith("/confirm"))).toBe(false);
  fireEvent.click(
    screen.getByRole("button", { name: "Xác nhận thêm 30 phút" }),
  );
  await waitFor(() => expect(changed).toHaveBeenCalled());
  expect(api).toHaveBeenCalledWith(
    "/api/sessions/extra-time/extra/confirm",
    "POST",
    { revision: "r" },
  );
  expect(await screen.findByText("Đã thêm 30 phút")).toBeTruthy();
});
it("explains a blocked extension without allowing confirmation", async () => {
  api.mockImplementation(async (url) =>
    url.endsWith("/time-review")
      ? [review]
      : {
          ...preview,
          id: "",
          feasible: false,
          slots: [],
          message: "Không đủ giờ trước deadline",
        },
  );
  render(<WorkTimeReviewCard />);
  await screen.findByText("Bạn đã làm xong chưa?");
  fireEvent.click(
    screen.getByRole("button", { name: "NORA đề xuất thêm giờ" }),
  );
  expect(await screen.findByText("Không đủ giờ trước deadline")).toBeTruthy();
  expect(screen.queryByRole("button", { name: /Xác nhận thêm/ })).toBeNull();
});
it("lets the user change the extra minutes and discards an old proposal", async () => {
  api.mockImplementation(async (url) =>
    url.endsWith("/time-review") ? [review] : preview,
  );
  render(<WorkTimeReviewCard />);
  await screen.findByText("Bạn đã làm xong chưa?");
  fireEvent.click(
    screen.getByRole("button", { name: "NORA đề xuất thêm giờ" }),
  );
  await screen.findByRole("button", { name: "Xác nhận thêm 30 phút" });
  fireEvent.click(screen.getByRole("button", { name: "+60 phút" }));
  expect(screen.queryByRole("button", { name: /Xác nhận thêm/ })).toBeNull();
  fireEvent.click(
    screen.getByRole("button", { name: "NORA đề xuất thêm giờ" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/sessions/source/extra-time/preview",
      "POST",
      { minutes: 60 },
    ),
  );
});
it("finishes the entire task only after explicitly confirming and refreshes the schedule", async () => {
  api.mockResolvedValue({});
  const changed = vi.fn();
  render(<FinishWorkButton sessionId="active" onWorkspaceChanged={changed} />);
  fireEvent.click(screen.getByRole("button", { name: "Đã xong công việc" }));
  expect(api).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Tiếp tục làm" }));
  expect(api).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Đã xong công việc" }));
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận hoàn thành" }));
  await waitFor(() => expect(changed).toHaveBeenCalled());
  expect(api).toHaveBeenCalledWith("/api/sessions/active/finish-task", "POST");
});
