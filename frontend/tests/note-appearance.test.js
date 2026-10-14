import { afterEach, expect, it, vi } from "vitest";
import {
  defaultNoteAppearance,
  normalizeNoteAppearance,
  readNoteAppearance,
  saveNoteAppearance,
  noteAppearanceStyle,
} from "../src/note-appearance.js";

afterEach(() => {
  localStorage.clear();
  vi.restoreAllMocks();
});

it("isolates appearance by account and note and rejects malformed stored preferences", () => {
  saveNoteAppearance("account-a", "note-1", {
    ...defaultNoteAppearance,
    paper: "#fff8e8",
    size: 24,
  });
  expect(readNoteAppearance("account-a", "note-1").size).toBe(24);
  expect(readNoteAppearance("account-b", "note-1")).toEqual(
    defaultNoteAppearance,
  );
  expect(readNoteAppearance("account-a", "note-2")).toEqual(
    defaultNoteAppearance,
  );
  localStorage.setItem(
    "planora.note-appearance.v1.account-a.note-1",
    "broken-json",
  );
  expect(readNoteAppearance("account-a", "note-1")).toEqual(
    defaultNoteAppearance,
  );
  expect(
    normalizeNoteAppearance({
      font: "url(fake)",
      size: 999,
      paper: "red;display:none",
      ink: null,
      ruled: "true",
    }),
  ).toEqual(defaultNoteAppearance);
});

it("keeps the editor usable when browser storage is blocked", () => {
  vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
    throw new Error("blocked");
  });
  vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
    throw new Error("blocked");
  });
  expect(readNoteAppearance("account", "note")).toEqual(defaultNoteAppearance);
  expect(() =>
    saveNoteAppearance("account", "note", defaultNoteAppearance),
  ).not.toThrow();
});

it("keeps paper metadata legible when a custom dark paper is selected", () => {
  const style = noteAppearanceStyle({
    ...defaultNoteAppearance,
    paper: "#171717",
    ink: "#fafafa",
  });
  expect(style["--note-paper-muted"]).toBe("#c8d2e0");
  expect(style["--note-paper-line"]).toBe("#ffffff20");
  expect(style["--note-ink"]).toBe("#fafafa");
});
