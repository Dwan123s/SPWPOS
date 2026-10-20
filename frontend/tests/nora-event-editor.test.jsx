import { beforeEach, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NoraEventEditor } from "../src/nora-event-editor.jsx";
import { state } from "../src/ui.jsx";
const event = {
  id: "work",
  name: "Đi làm",
  start: "2026-10-20T06:00:00Z",
  end: "2026-10-20T10:00:00Z",
  eventType: "WORK",
  description: "Giữ mô tả này",
};
beforeEach(() => {
  state.workspace = { preference: { timeZoneId: "Asia/Ho_Chi_Minh" } };
});
it("opens existing fields and cancellation does not save", async () => {
  const fetch = vi.fn();
  vi.stubGlobal("fetch", fetch);
  let closed = false;
  render(
    <NoraEventEditor
      event={event}
      onCancel={() => {
        closed = true;
      }}
      onSaved={() => {}}
    />,
  );
  expect(screen.getByLabelText("Tên sự kiện").value).toBe("Đi làm");
  expect(screen.getByLabelText("Bắt đầu").value).toBe("2026-10-20T13:00");
  await userEvent.click(screen.getByRole("button", { name: "Hủy" }));
  expect(closed).toBe(true);
  expect(fetch).not.toHaveBeenCalled();
});
it("retains edits after an overlap rejection, retries, and preserves category and description", async () => {
  let rejected = true,
    saved = false,
    sent;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options) => {
      sent = { url, body: JSON.parse(options.body) };
      return {
        ok: !rejected,
        status: rejected ? 409 : 200,
        json: async () =>
          rejected
            ? { error: { message: "Trùng lịch với cuộc họp." } }
            : { data: event },
      };
    }),
  );
  render(
    <NoraEventEditor
      event={event}
      onCancel={() => {}}
      onSaved={() => {
        saved = true;
      }}
    />,
  );
  await userEvent.clear(screen.getByLabelText("Tên sự kiện"));
  await userEvent.type(screen.getByLabelText("Tên sự kiện"), "Đi làm ca chiều");
  fireEvent.change(screen.getByLabelText("Bắt đầu"), {
    target: { value: "2026-10-20T13:30" },
  });
  await userEvent.click(screen.getByRole("button", { name: "Lưu thay đổi" }));
  await screen.findByRole("alert");
  expect(saved).toBe(false);
  expect(screen.getByLabelText("Tên sự kiện").value).toBe("Đi làm ca chiều");
  rejected = false;
  await userEvent.click(screen.getByRole("button", { name: "Lưu thay đổi" }));
  expect(saved).toBe(true);
  expect(sent.url).toBe("/api/time/events/work");
  expect(sent.body).toMatchObject({
    name: "Đi làm ca chiều",
    start: "2026-10-20T06:30:00.000Z",
    end: new Date(event.end).toISOString(),
    eventType: "WORK",
    description: event.description,
    repeatWeeks: 1,
  });
});

it("dismisses on the backdrop but preserves inside clicks and drags starting inside", () => {
  const cancel = vi.fn();
  render(
    <NoraEventEditor event={event} onCancel={cancel} onSaved={() => {}} />,
  );
  const dialog = screen.getByRole("dialog");
  vi.spyOn(dialog, "getBoundingClientRect").mockReturnValue({
    left: 100,
    right: 500,
    top: 100,
    bottom: 400,
  });
  fireEvent.click(dialog, { clientX: 150, clientY: 150 });
  expect(cancel).not.toHaveBeenCalled();
  fireEvent.pointerDown(screen.getByLabelText("Tên sự kiện"), {
    clientX: 150,
    clientY: 150,
  });
  fireEvent.click(dialog, { clientX: 20, clientY: 20 });
  expect(cancel).not.toHaveBeenCalled();
  fireEvent.pointerDown(dialog, { clientX: 20, clientY: 20 });
  fireEvent.click(dialog, { clientX: 20, clientY: 20 });
  expect(cancel).toHaveBeenCalledOnce();
});
