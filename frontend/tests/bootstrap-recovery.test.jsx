import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { App } from "../src/App.jsx";
import { state } from "../src/ui.jsx";
import { update } from "../src/store.js";
import { workspace, insights, profile } from "./fixtures.js";

beforeEach(() => {
  Object.assign(state, {
    workspace: null,
    user: null,
    roles: [],
    insights: null,
  });
  history.replaceState(null, "", "/#today");
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    slots: {},
    toasts: [],
  });
});

it("keeps a server failure distinct from signed-out state and retries successfully", async () => {
  let unavailable = true;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url) => {
      if (unavailable)
        return {
          ok: false,
          status: 503,
          json: async () => ({
            error: { code: "UNAVAILABLE", message: "Service unavailable" },
          }),
        };
      const data =
        url === "/api/browser-session/me"
          ? { user: profile, roles: [] }
          : url === "/api/workspace"
            ? workspace()
            : url === "/api/intelligence"
              ? insights
              : [];
      return { ok: true, status: 200, json: async () => ({ data }) };
    }),
  );
  render(<App />);
  expect(
    await screen.findByRole("button", { name: "Thử kết nối lại" }),
  ).toBeTruthy();
  expect(screen.queryByLabelText("Tên đăng nhập hoặc email")).toBeNull();
  unavailable = false;
  fireEvent.click(screen.getByRole("button", { name: "Thử kết nối lại" }));
  expect(
    await screen.findByRole("heading", { name: /Một ngày có chủ đích/ }),
  ).toBeTruthy();
});

it("shows a retry state when the initial request fails at the network layer", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => {
      throw new TypeError("Failed to fetch");
    }),
  );
  render(<App />);
  expect(await screen.findByRole("alert")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Thử kết nối lại" })).toBeTruthy();
  expect(location.hash).toBe("#today");
});
