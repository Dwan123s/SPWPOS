import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ConversationLibrary } from "../src/conversations.jsx";

let rows, failEdit, failDelete;
beforeEach(() => {
  rows = [
    { id: "chat", title: "Kế hoạch tuần", archived: false, version: "v1" },
  ];
  failEdit = failDelete = false;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (raw, options = {}) => {
      const url = new URL(raw, "https://localhost");
      const method = options.method || "GET";
      let data;
      if (method === "PUT" || method === "DELETE") {
        if (
          (method === "PUT" && failEdit) ||
          (method === "DELETE" && failDelete)
        )
          return {
            ok: false,
            status: 503,
            json: async () => ({
              error: { message: "Chưa lưu được. Hãy thử lại." },
            }),
          };
        if (method === "DELETE") rows = [];
        else
          rows[0] = { ...rows[0], ...JSON.parse(options.body), version: "v2" };
        data = rows[0] || {};
      } else
        data = {
          items: rows.filter(
            (x) => x.archived === (url.searchParams.get("archived") === "true"),
          ),
          nextCursor: null,
        };
      return { ok: true, json: async () => ({ data }) };
    }),
  );
});
async function openOptions(user) {
  const trigger = await screen.findByRole("button", {
    name: "Tùy chọn Kế hoạch tuần",
  });
  await waitFor(() => expect(trigger.disabled).toBe(false));
  await user.click(trigger);
  return trigger;
}
it("opens conversation actions by keyboard, closes on Escape and restores focus", async () => {
  const user = userEvent.setup();
  render(
    <ConversationLibrary
      selected="chat"
      onChoose={() => {}}
      onRemoved={() => {}}
    />,
  );
  const trigger = await screen.findByRole("button", {
    name: "Tùy chọn Kế hoạch tuần",
  });
  trigger.focus();
  await user.keyboard("{ArrowDown}");
  expect(
    await screen.findByRole("menu", {
      name: "Tùy chọn hội thoại Kế hoạch tuần",
    }),
  ).toBeTruthy();
  expect(document.activeElement.textContent).toContain("Đổi tên");
  await user.keyboard("{Escape}");
  expect(screen.queryByRole("menu")).toBeNull();
  expect(document.activeElement).toBe(trigger);
});
it("preserves a failed rename draft and saves it on retry", async () => {
  const user = userEvent.setup();
  render(
    <ConversationLibrary
      selected="chat"
      onChoose={() => {}}
      onRemoved={() => {}}
    />,
  );
  await openOptions(user);
  await user.click(screen.getByRole("menuitem", { name: "Đổi tên" }));
  const input = screen.getByRole("textbox", { name: "Tên hội thoại" });
  await user.clear(input);
  await user.type(input, "Kế hoạch mới");
  failEdit = true;
  await user.click(screen.getByRole("button", { name: "Lưu tên" }));
  await screen.findByRole("alert");
  expect(screen.getByRole("textbox", { name: "Tên hội thoại" }).value).toBe(
    "Kế hoạch mới",
  );
  failEdit = false;
  await user.click(screen.getByRole("button", { name: "Lưu tên" }));
  expect(
    await screen.findByRole("button", { name: "Kế hoạch mới", exact: true }),
  ).toBeTruthy();
  expect(rows[0].title).toBe("Kế hoạch mới");
});
it("archives and restores a conversation from its own actions", async () => {
  const user = userEvent.setup();
  render(
    <ConversationLibrary
      selected="different"
      onChoose={() => {}}
      onRemoved={() => {}}
    />,
  );
  await openOptions(user);
  await user.click(
    screen.getByRole("menuitem", { name: "Lưu trữ", exact: true }),
  );
  await waitFor(() => expect(rows[0].archived).toBe(true));
  await user.click(screen.getByRole("tab", { name: "Đã lưu trữ" }));
  await openOptions(user);
  await user.click(
    screen.getByRole("menuitem", { name: "Khôi phục", exact: true }),
  );
  await waitFor(() => expect(rows[0].archived).toBe(false));
  await user.click(screen.getByRole("tab", { name: "Gần đây" }));
  expect(
    await screen.findByRole("button", { name: "Kế hoạch tuần", exact: true }),
  ).toBeTruthy();
});
it("keeps a failed deletion visible for retry instead of pretending it succeeded", async () => {
  const user = userEvent.setup();
  render(
    <ConversationLibrary
      selected="chat"
      onChoose={() => {}}
      onRemoved={() => {}}
    />,
  );
  await openOptions(user);
  await user.click(screen.getByRole("menuitem", { name: "Xóa", exact: true }));
  failDelete = true;
  await user.click(
    within(
      screen.getByRole("alertdialog", { name: "Xóa cuộc trò chuyện?" }),
    ).getByRole("button", { name: "Xóa vĩnh viễn" }),
  );
  await screen.findByRole("alert");
  expect(screen.getByText("Xóa cuộc trò chuyện?")).toBeTruthy();
  expect(rows).toHaveLength(1);
  failDelete = false;
  await user.click(
    within(
      screen.getByRole("alertdialog", { name: "Xóa cuộc trò chuyện?" }),
    ).getByRole("button", { name: "Xóa vĩnh viễn" }),
  );
  await waitFor(() => expect(rows).toHaveLength(0));
  expect(
    await screen.findByText("Hội thoại của bạn sẽ xuất hiện ở đây."),
  ).toBeTruthy();
});

it("identifies the selected delete target and cancels with Escape without deleting it", async () => {
  const user = userEvent.setup();
  render(
    <ConversationLibrary
      selected="chat"
      onChoose={() => {}}
      onRemoved={() => {}}
    />,
  );
  const trigger = await openOptions(user);
  await user.click(screen.getByRole("menuitem", { name: "Xóa", exact: true }));
  const dialog = screen.getByRole("alertdialog", {
    name: "Xóa cuộc trò chuyện?",
  });
  expect(dialog.textContent).toContain("Kế hoạch tuần");
  expect(document.activeElement).toBe(
    within(dialog).getByRole("button", { name: "Hủy" }),
  );
  await user.keyboard("{Escape}");
  expect(screen.queryByRole("alertdialog")).toBeNull();
  expect(rows).toHaveLength(1);
  expect(document.activeElement).toBe(trigger);
});

it("closes conversation actions even when the outside click stops propagation", async () => {
  const user = userEvent.setup();
  render(
    <>
      <ConversationLibrary
        selected="chat"
        onChoose={() => {}}
        onRemoved={() => {}}
      />
      <div onClick={(event) => event.stopPropagation()}>Outside surface</div>
    </>,
  );
  await openOptions(user);
  fireEvent.click(screen.getByText("Outside surface"));
  expect(screen.queryByRole("menu")).toBeNull();
});
