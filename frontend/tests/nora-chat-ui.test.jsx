import { beforeEach, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import {
  NoraReply,
  groupConversations,
  planHours,
  planDay,
  planSessionName,
} from "../src/nora-chat-ui.jsx";
import { state } from "../src/ui.jsx";
beforeEach(() => {
  state.workspace = {
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    tasks: [],
  };
});
it("renders useful answer formatting while treating raw HTML as text", () => {
  const { container } = render(
    <NoraReply
      text={
        "### Kế hoạch\n\n**Báo cáo SWP**\n\n- Làm báo cáo\n- Kiểm tra deadline\n\n1. Mở lịch\n2. Xác nhận\n\n```text\n<img src=x onerror=alert(1)>\n```"
      }
    />,
  );
  expect(screen.getByRole("heading", { name: "Kế hoạch" })).toBeTruthy();
  expect(container.querySelector("strong").textContent).toBe("Báo cáo SWP");
  expect(screen.getAllByRole("list")).toHaveLength(2);
  expect(container.querySelector("img")).toBeNull();
  expect(container.querySelector("pre").textContent).toContain("<img src=x");
});
it("only links safe external URLs and known app routes, and rejects executable URLs", () => {
  render(
    <NoraReply
      text={
        "[Lịch](#calendar) [Google](https://example.com/test) [Xấu](javascript:alert) [Dữ liệu](data:text/html,hello) [Lạ](#unexpected)"
      }
    />,
  );
  const links = screen.getAllByRole("link");
  expect(links).toHaveLength(2);
  expect(links[0].getAttribute("href")).toBe("#calendar");
  expect(links[1].getAttribute("rel")).toBe("noopener noreferrer");
  expect(screen.getByText(/Xấu/)).toBeTruthy();
});
it("keeps incomplete formatting readable as streaming text arrives", () => {
  const { rerender, container } = render(
    <NoraReply text="Đang xếp **báo" streaming />,
  );
  expect(screen.getByText("Đang xếp **báo")).toBeTruthy();
  rerender(<NoraReply text="Đang xếp **báo cáo**" streaming />);
  expect(container.querySelector("strong").textContent).toBe("báo cáo");
  expect(container.querySelector(".nora-stream-caret")).toBeTruthy();
});
it("groups history in the account timezone and searches follow-up answers while preserving the first title", () => {
  const turns = [
    {
      conversationId: "one",
      text: "Báo cáo",
      reply: "",
      createdAt: "2026-10-14T09:00:00Z",
    },
    {
      conversationId: "two",
      text: "Cuộc họp",
      reply: "",
      createdAt: "2026-10-14T10:00:00Z",
    },
    {
      conversationId: "one",
      text: "Làm trước 18 giờ",
      reply: "Tập trung hai giờ",
      createdAt: "2026-10-14T17:30:00Z",
    },
  ];
  const groups = groupConversations(
    turns,
    "",
    new Date("2026-10-14T18:00:00Z"),
  );
  expect(groups.map((group) => group.name)).toEqual(["Hôm nay", "Hôm qua"]);
  const found = groupConversations(
    turns,
    "tập trung",
    new Date("2026-10-14T18:00:00Z"),
  );
  expect(found[0].items[0].title).toBe("Báo cáo");
  expect(found[0].items[0].count).toBe(2);
});
it("does not invent dates for older history without a timestamp", () => {
  expect(
    groupConversations([
      { conversationId: "old", text: "Học", reply: "", createdAt: "invalid" },
    ])[0].name,
  ).toBe("Gần đây");
});
it("shows actual schedule times and names from the result or current workspace", () => {
  expect(planHours("2026-10-15T00:00:00Z")).toBe("07:00");
  expect(planDay("2026-10-15T00:00:00Z")).toContain("15/10");
  expect(
    planSessionName(
      { taskId: "report" },
      { changes: [{ taskId: "report", name: "Báo cáo SWP" }] },
    ),
  ).toBe("Báo cáo SWP");
});
