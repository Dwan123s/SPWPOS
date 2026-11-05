import { afterEach, expect, it, vi } from "vitest";

vi.mock("../src/ui.jsx", () => ({ toast: vi.fn() }));
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

it("respects Retry-After without retrying writes or expiring the session", async () => {
  vi.resetModules();
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-11-04T00:00:00Z"));
  const fetch = vi
    .fn()
    .mockResolvedValueOnce({
      ok: false,
      status: 429,
      headers: new Headers({ "Retry-After": "60" }),
      json: async () => ({
        error: { code: "RATE_LIMITED", message: "Too many requests." },
      }),
    })
    .mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: [] }),
    });
  vi.stubGlobal("fetch", fetch);
  const expired = vi.fn();
  window.addEventListener("session-expired", expired);
  try {
    const { api } = await import("../src/api.js");
    await expect(
      api("/api/teams/demo/briefs", "POST", { text: "demo" }),
    ).rejects.toMatchObject({
      status: 429,
      code: "RATE_LIMITED",
      retryAfter: 60,
    });
    await vi.advanceTimersByTimeAsync(7000);
    await expect(
      api("/api/teams/demo/briefs", "POST", { text: "demo" }),
    ).rejects.toMatchObject({ status: 429, retryAfter: 53 });
    expect(fetch).toHaveBeenCalledTimes(1);
    // Unrelated reads remain usable during an action's cooldown.
    await expect(api("/api/teams")).resolves.toEqual([]);
    await vi.advanceTimersByTimeAsync(53000);
    expect(fetch).toHaveBeenCalledTimes(2);
    await expect(
      api("/api/teams/demo/briefs", "POST", { text: "demo" }),
    ).resolves.toEqual([]);
    expect(fetch).toHaveBeenCalledTimes(3);
    expect(expired).not.toHaveBeenCalled();
  } finally {
    window.removeEventListener("session-expired", expired);
  }
});

it("pauses polling with changing query cursors and preserves specific limit messages", async () => {
  vi.resetModules();
  vi.useFakeTimers();
  const fetch = vi.fn().mockResolvedValue({
    ok: false,
    status: 429,
    headers: new Headers({ "Retry-After": "30" }),
    json: async () => ({
      error: { code: "AI_DAILY_LIMIT", message: "Đã hết lượt AI hôm nay." },
    }),
  });
  vi.stubGlobal("fetch", fetch);
  const { api } = await import("../src/api.js");
  await expect(api("/api/teams/demo/messages?after=1")).rejects.toThrow(
    "Đã hết lượt AI hôm nay.",
  );
  await expect(api("/api/teams/demo/messages?after=2")).rejects.toMatchObject({
    code: "AI_DAILY_LIMIT",
    retryAfter: 30,
  });
  expect(fetch).toHaveBeenCalledTimes(1);
});
