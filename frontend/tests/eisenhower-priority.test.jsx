import { expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { eisenhowerPriority } from "../src/eisenhower-priority.js";
import { EisenhowerPriorityField } from "../src/eisenhower-priority.jsx";

const now = Date.parse("2026-11-18T06:00:00Z");
it.each([
  ["Important", 24, "Q1"], ["Important", 24.001, "Q2"],
  ["Lower", -1, "Q3"], ["Lower", 48, "Q4"], ["Unknown", 1, "Unknown"],
])("classifies %s at %s hours using only importance and deadline", (importanceLevel, hours, quadrant) => {
  expect(eisenhowerPriority({ importanceLevel, deadline: new Date(now + hours * 3600000).toISOString(), priority: 4, urgencyOverride: "NotUrgent" }, now).quadrant).toBe(quadrant);
});
it("keeps undated and legacy tasks unclassified until their importance and deadline are known", () => {
  expect(eisenhowerPriority({ importanceLevel: "Important" }, now).urgency).toBe("Unknown");
  expect(eisenhowerPriority({ priority: 4, deadline: new Date(now).toISOString() }, now).quadrant).toBe("Unknown");
});
it("updates the displayed quadrant when the form deadline changes without offering manual urgency", () => {
  vi.spyOn(Date, "now").mockReturnValue(now);
  render(<form><label>Ngày<input name="deadline" type="date" defaultValue="2026-11-20" /></label>
    <input type="hidden" name="deadlineTime" value="17:00" />
    <EisenhowerPriorityField />
  </form>);
  fireEvent.change(screen.getByRole("combobox", { name: "Mức quan trọng (Eisenhower)" }), { target: { value: "Important" } });
  fireEvent.change(screen.getByLabelText("Ngày"), { target: { value: "2026-11-18" } });
  expect(screen.getByText(/Q1 · Làm trước/)).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Ngày"), { target: { value: "2026-11-20" } });
  expect(screen.getByText(/Q2 · Chủ động lên lịch/)).toBeTruthy();
  expect(screen.getAllByRole("combobox")).toHaveLength(1);
});
