import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { WorkspaceMotionControl } from "../src/workspace-motion.jsx";

const preferenceKey = "planora.workspace.motion-paused";
const cancel = vi.fn();
const animate = vi.fn(() => ({ cancel, finished: new Promise(() => {}) }));
function mount() {
  return render(
    <div className="glass-workspace">
      <WorkspaceMotionControl />
      <div className="page-header">Nội dung luôn sẵn sàng</div>
    </div>,
  );
}
beforeEach(() => {
  localStorage.removeItem(preferenceKey);
  animate.mockClear();
  cancel.mockClear();
  Object.defineProperty(HTMLElement.prototype, "animate", {
    configurable: true,
    value: animate,
  });
});
afterEach(() => {
  delete HTMLElement.prototype.animate;
  localStorage.removeItem(preferenceKey);
  vi.restoreAllMocks();
});

it("persists the motion choice across page mounts and finishes reveals when paused", () => {
  const first = mount();
  expect(animate).toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Tạm dừng chuyển động" }));
  expect(document.querySelector(".glass-workspace").dataset.motionState).toBe(
    "paused",
  );
  expect(cancel).toHaveBeenCalled();
  expect(localStorage.getItem(preferenceKey)).toBe("true");
  first.unmount();
  animate.mockClear();
  mount();
  expect(animate).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Bật chuyển động" }));
  expect(document.querySelector(".glass-workspace").dataset.motionState).toBe(
    "running",
  );
  expect(localStorage.getItem(preferenceKey)).toBe("false");
});

it("honors the OS reduced-motion setting and its changes", () => {
  let change;
  const media = {
    matches: true,
    addEventListener: (_, callback) => (change = callback),
    removeEventListener: vi.fn(),
  };
  vi.spyOn(window, "matchMedia").mockReturnValue(media);
  const view = mount();
  expect(
    screen.getByRole("button", { name: "Hệ thống đang giảm chuyển động" })
      .disabled,
  ).toBe(true);
  expect(animate).not.toHaveBeenCalled();
  act(() => {
    media.matches = false;
    change();
  });
  expect(document.querySelector(".glass-workspace").dataset.motionState).toBe(
    "running",
  );
  expect(
    screen.getByRole("button", { name: "Tạm dừng chuyển động" }).disabled,
  ).toBe(false);
  view.unmount();
  expect(media.removeEventListener).toHaveBeenCalledWith("change", change);
});

it("finishes pending reveals when a tab is hidden and resumes ambient motion when visible", () => {
  const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  mount();
  hidden.mockReturnValue(true);
  fireEvent(document, new Event("visibilitychange"));
  expect(document.querySelector(".glass-workspace").dataset.motionState).toBe(
    "paused",
  );
  expect(cancel).toHaveBeenCalled();
  hidden.mockReturnValue(false);
  fireEvent(document, new Event("visibilitychange"));
  expect(document.querySelector(".glass-workspace").dataset.motionState).toBe(
    "running",
  );
});

it("keeps content usable when native animation is unavailable", () => {
  delete HTMLElement.prototype.animate;
  mount();
  expect(screen.getByText("Nội dung luôn sẵn sàng").style.opacity).toBe("");
  fireEvent.click(screen.getByRole("button", { name: "Tạm dừng chuyển động" }));
  expect(
    screen
      .getByRole("button", { name: "Bật chuyển động" })
      .getAttribute("aria-pressed"),
  ).toBe("true");
});

it("reveals dashboard and asynchronous surfaces once without animating timer updates", async () => {
  mount();
  animate.mockClear();
  const card = document.createElement("section");
  card.className = "focus-cockpit";
  document.querySelector(".glass-workspace").append(card);
  await waitFor(() => expect(animate).toHaveBeenCalledOnce());
  card.append(document.createElement("span"));
  await act(async () => {});
  expect(animate).toHaveBeenCalledOnce();
});

it("keeps asynchronously loaded cards visible while motion is paused", async () => {
  localStorage.setItem(preferenceKey, "true");
  mount();
  const card = document.createElement("section");
  card.className = "focus-cockpit";
  document.querySelector(".glass-workspace").append(card);
  await act(async () => {});
  expect(animate).not.toHaveBeenCalled();
  expect(card.style.opacity).toBe("");
});
