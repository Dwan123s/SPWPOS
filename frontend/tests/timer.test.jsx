import { it, expect, vi } from "vitest";
import { act, render, screen } from "@testing-library/react";
import { state } from "../src/ui.jsx";
import { SessionTimer } from "../src/react-ui.jsx";

it("updates running elapsed time, preserves paused duration and cleans up its timer", () => {
  vi.useFakeTimers();
  try {
    const session = { id: "running", actualSeconds: 90, status: "Running" };
    state.workspace = { sessions: [session] };
    state.loadedAt = Date.now();
    const view = render(<SessionTimer id="running" />);
    expect(screen.getByText("00:01:30")).toBeTruthy();
    act(() => vi.advanceTimersByTime(2000));
    expect(screen.getByText("00:01:32")).toBeTruthy();
    session.status = "Paused";
    session.actualSeconds = 92;
    act(() => vi.advanceTimersByTime(3000));
    expect(screen.getByText("00:01:32")).toBeTruthy();
    view.unmount();
    expect(vi.getTimerCount()).toBe(0);
  } finally {
    vi.useRealTimers();
  }
});
