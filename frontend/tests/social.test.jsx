import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
  FriendsPage,
  FriendshipActions,
  PeopleList,
  ProfilePage,
} from "../src/social.jsx";
import { api } from "../src/api.js";
import { state } from "../src/ui.jsx";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
const person = {
  id: "11111111-1111-1111-1111-111111111111",
  displayName: "Nguyễn Minh",
  bio: "Thích đọc sách",
  avatarUrl: "",
  relationship: "None",
};
const page = (items, nextOffset = null) => ({
  items,
  nextOffset,
  hasMore: nextOffset !== null,
});
const profile = {
  ...person,
  coverUrl: "",
  joinedAt: "2026-10-01T08:00:00Z",
  friendCount: 2,
  mutualFriendCount: 1,
};
beforeEach(() => {
  api.mockReset();
  state.workspace = { profile: { id: "me", displayName: "Tôi" } };
  history.replaceState(null, "", "/#friends");
});

it("finds people by display name and updates a sent request without creating a friendship prematurely", async () => {
  const user = userEvent.setup();
  let relationship = "None";
  api.mockImplementation(async (url, method) => {
    if (method === "POST") {
      relationship = "Outgoing";
      return { relationship };
    }
    return page([{ ...person, relationship }]);
  });
  render(<FriendsPage />);
  expect(await screen.findByRole("link", { name: /Nguyễn Minh/ })).toBeTruthy();
  await user.click(screen.getByRole("button", { name: "Thêm bạn bè" }));
  expect(api).toHaveBeenCalledWith(
    `/api/social/friends/${person.id}`,
    "POST",
    {},
  );
  await screen.findByRole("button", { name: "Thu hồi lời mời" });
  await user.type(
    screen.getByRole("textbox", { name: "Tìm người theo tên" }),
    "Minh",
  );
  await user.click(screen.getByRole("button", { name: "Tìm kiếm" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/social/people?search=Minh&offset=0"),
  );
});

it("opens received invitations directly and accepting removes the invitation from that list", async () => {
  const user = userEvent.setup();
  let accepted = false;
  api.mockImplementation(async (url, method) => {
    if (method === "POST") {
      accepted = true;
      return { relationship: "Friends" };
    }
    return page(accepted ? [] : [{ ...person, relationship: "Incoming" }]);
  });
  render(<FriendsPage friendsTab="incoming" />);
  await user.click(await screen.findByRole("button", { name: "Chấp nhận" }));
  expect(api).toHaveBeenCalledWith(
    `/api/social/friends/${person.id}/accept`,
    "POST",
    {},
  );
  await screen.findByText("Chưa có lời mời mới");
  expect(screen.queryByRole("button", { name: "Thêm bạn bè" })).toBeNull();
});

it.each([
  ["Incoming", "Từ chối"],
  ["Outgoing", "Thu hồi lời mời"],
])("allows cancelling a %s request", async (relationship, label) => {
  const user = userEvent.setup();
  const changed = vi.fn();
  api.mockResolvedValue({ relationship: "None" });
  render(
    <FriendshipActions
      person={{ ...person, relationship }}
      onChanged={changed}
    />,
  );
  await user.click(screen.getByRole("button", { name: label }));
  expect(api).toHaveBeenCalledWith(
    `/api/social/friends/${person.id}`,
    "DELETE",
    undefined,
  );
  expect(changed).toHaveBeenCalledWith("None");
});

it("requires confirmation to unfriend and preserves the relationship when the server refuses", async () => {
  const user = userEvent.setup();
  const changed = vi.fn();
  api.mockRejectedValue(new Error("Chưa hủy được kết bạn. Hãy thử lại."));
  render(
    <FriendshipActions
      person={{ ...person, relationship: "Friends" }}
      onChanged={changed}
    />,
  );
  await user.click(screen.getByRole("button", { name: "Bạn bè" }));
  expect(api).not.toHaveBeenCalled();
  await user.click(screen.getByRole("button", { name: "Hủy kết bạn" }));
  expect(await screen.findByRole("alert")).toBeTruthy();
  expect(changed).not.toHaveBeenCalled();
  await user.click(screen.getByRole("button", { name: "Giữ lại" }));
  expect(screen.getByRole("button", { name: "Bạn bè" })).toBeTruthy();
});

it("shows another person's profile and friends without exposing edit actions", async () => {
  const user = userEvent.setup();
  api.mockImplementation(async (url) =>
    url.endsWith("/friends?offset=0") ? page([]) : profile,
  );
  render(<ProfilePage profileId={person.id} />);
  expect(
    await screen.findByRole("heading", { name: person.displayName }),
  ).toBeTruthy();
  expect(screen.getByText("Thích đọc sách")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: /2 bạn bè · 1 bạn chung/ }),
  ).toBeTruthy();
  expect(screen.queryByRole("link", { name: "Chỉnh sửa hồ sơ" })).toBeNull();
  expect(screen.queryByRole("button", { name: "Thêm ảnh bìa" })).toBeNull();
  await user.click(screen.getByRole("button", { name: "Bạn bè 2" }));
  await screen.findByText("Chưa có bạn bè");
  expect(api).toHaveBeenCalledWith(
    `/api/social/profiles/${person.id}/friends?offset=0`,
  );
});

it("lets the owner edit their profile and remove their own cover", async () => {
  const user = userEvent.setup();
  api.mockImplementation(async (url, method) =>
    method === "PUT"
      ? {}
      : {
          ...profile,
          id: "me",
          relationship: "Self",
          coverUrl: "data:image/png;base64,AAA",
        },
  );
  render(<ProfilePage />);
  expect(
    (await screen.findByRole("link", { name: "Chỉnh sửa hồ sơ" })).getAttribute(
      "href",
    ),
  ).toBe("#settings");
  expect(screen.queryByRole("button", { name: "Thêm bạn bè" })).toBeNull();
  await user.click(screen.getByRole("button", { name: "Gỡ ảnh bìa" }));
  await screen.findByRole("button", { name: "Thêm ảnh bìa" });
  expect(api).toHaveBeenCalledWith("/api/social/profile/cover", "PUT", {
    coverUrl: "",
  });
  expect(screen.getByRole("status").textContent).toBe("Đã gỡ ảnh bìa.");
});

it("rejects oversized cover images before uploading", async () => {
  const user = userEvent.setup();
  api.mockResolvedValue({ ...profile, id: "me", relationship: "Self" });
  render(<ProfilePage />);
  await screen.findByRole("button", { name: "Thêm ảnh bìa" });
  const file = new File([new Uint8Array(8 * 1024 * 1024 + 1)], "large.png", {
    type: "image/png",
  });
  await user.upload(screen.getByLabelText("Chọn ảnh bìa"), file);
  expect((await screen.findByRole("alert")).textContent).toContain(
    "tối đa 8 MB",
  );
  expect(api.mock.calls.some(([, method]) => method === "PUT")).toBe(false);
});

it("appends pages without dropping earlier people and offers retry after a load failure", async () => {
  const user = userEvent.setup();
  let attempts = 0;
  api.mockImplementation(async (url) => {
    if (url.endsWith("offset=0")) return page([person], 24);
    if (++attempts === 1) throw new Error("Mất kết nối tạm thời");
    return page([{ ...person, id: "second", displayName: "Bảo" }]);
  });
  render(<PeopleList endpoint="/api/social/people" emptyTitle="Trống" />);
  await user.click(await screen.findByRole("button", { name: "Xem thêm" }));
  expect(await screen.findByText("Mất kết nối tạm thời")).toBeTruthy();
  await user.click(screen.getByRole("button", { name: "Thử lại" }));
  await screen.findByText("Bảo", { selector: "strong" });
  expect(document.querySelectorAll(".social-person-card")).toHaveLength(2);
  expect(screen.getByRole("link", { name: /Nguyễn Minh/ })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Xem thêm" })).toBeNull();
});

it("does not display an old search response after switching to invitations", async () => {
  const user = userEvent.setup();
  let finishSearch;
  api.mockImplementation((url) =>
    url.includes("/people?")
      ? new Promise((resolve) => {
          finishSearch = resolve;
        })
      : Promise.resolve(page([])),
  );
  render(<FriendsPage />);
  await user.click(
    within(
      screen.getByRole("navigation", { name: "Danh sách bạn bè" }),
    ).getByRole("button", { name: /Lời mời đã nhận/ }),
  );
  await screen.findByText("Chưa có lời mời mới");
  await act(async () => finishSearch(page([person])));
  expect(screen.queryByText(person.displayName)).toBeNull();
  expect(location.hash).toBe("#friends/incoming");
});
