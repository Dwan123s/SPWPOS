import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { NotificationSoundSettings } from "../src/notification-sound-settings.jsx";
import {
  notificationSoundEnabled,
  setNotificationSoundEnabled,
  startNotificationSound,
} from "../src/notification-sound.js";
import {
  syncNotifications,
  resetNotificationFeed,
  previewNotificationPopup,
  notificationSnapshot,
} from "../src/notification-feed.js";

let stop, current, audio;
const notice = (id) => ({
  id,
  isRead: false,
  title: id,
  createdAt: "2026-11-03T00:00:00Z",
});
const sync = (items) => syncNotifications({ ...current, notifications: items });
beforeEach(() => {
  localStorage.clear();
  resetNotificationFeed();
  setNotificationSoundEnabled("listener", true);
  current = {
    profile: { id: "listener" },
    notifications: [],
    notificationSettings: { inAppEnabled: true },
  };
  const parameter = () => ({
    setValueAtTime: vi.fn(),
    linearRampToValueAtTime: vi.fn(),
    exponentialRampToValueAtTime: vi.fn(),
  });
  audio = {
    state: "running",
    currentTime: 10,
    destination: {},
    resume: vi.fn(async () => {
      audio.state = "running";
    }),
    close: vi.fn(async () => {
      audio.state = "closed";
    }),
    createOscillator: vi.fn(() => ({
      frequency: parameter(),
      connect: vi.fn(),
      disconnect: vi.fn(),
      start: vi.fn(),
      stop: vi.fn(),
    })),
    createGain: vi.fn(() => ({
      gain: parameter(),
      connect: vi.fn(),
      disconnect: vi.fn(),
    })),
  };
  vi.stubGlobal(
    "AudioContext",
    vi.fn(function () {
      return audio;
    }),
  );
  stop = startNotificationSound(() => current);
});
afterEach(() => {
  stop();
  resetNotificationFeed();
  vi.unstubAllGlobals();
});

it("waits for user interaction, stays silent for history, then chimes once for a batch of new notices", () => {
  sync([notice("old")]);
  expect(window.AudioContext).not.toHaveBeenCalled();
  fireEvent.pointerDown(document);
  expect(window.AudioContext).toHaveBeenCalledTimes(1);
  expect(audio.createOscillator).not.toHaveBeenCalled();
  sync([notice("new-1"), notice("new-2"), notice("old")]);
  expect(audio.createOscillator).toHaveBeenCalledTimes(2);
  sync([notice("new-1"), notice("new-2"), notice("old")]);
  expect(audio.createOscillator).toHaveBeenCalledTimes(2);
  const tone = audio.createOscillator.mock.results[0].value;
  expect(tone.start).toHaveBeenCalledWith(10);
  expect(tone.stop).toHaveBeenCalledWith(10.24);
});

it("does not replay pre-interaction notifications when audio is unlocked", () => {
  sync([]);
  sync([notice("before-click")]);
  fireEvent.keyDown(document, { key: "Tab" });
  sync([notice("before-click")]);
  expect(audio.createOscillator).not.toHaveBeenCalled();
  sync([notice("after-click"), notice("before-click")]);
  expect(audio.createOscillator).toHaveBeenCalledTimes(2);
});

it("honors mute, in-app opt-out, and account switching without playing old notifications", () => {
  fireEvent.pointerDown(document);
  sync([]);
  setNotificationSoundEnabled("listener", false);
  sync([notice("muted")]);
  setNotificationSoundEnabled("listener", true);
  sync([notice("muted")]);
  current.notificationSettings.inAppEnabled = false;
  sync([notice("disabled"), notice("muted")]);
  current.notificationSettings.inAppEnabled = true;
  sync([notice("disabled"), notice("muted")]);
  current = { ...current, profile: { id: "another-account" } };
  sync([notice("other-history")]);
  expect(audio.createOscillator).not.toHaveBeenCalled();
  sync([notice("other-new"), notice("other-history")]);
  expect(audio.createOscillator).toHaveBeenCalledTimes(2);
});

it("stores an account-specific browser preference and previews sound without creating inbox entries", () => {
  sync([]);
  fireEvent.pointerDown(document);
  render(<NotificationSoundSettings userId="listener" />);
  fireEvent.click(screen.getByRole("button", { name: "Nghe thử âm báo" }));
  expect(audio.createOscillator).toHaveBeenCalledTimes(2);
  fireEvent.click(screen.getByRole("checkbox", { name: "Âm thanh thông báo" }));
  expect(localStorage.getItem("planora.notification-sound.listener")).toBe(
    "false",
  );
  expect(notificationSoundEnabled("fresh-account")).toBe(true);
  expect(screen.getByRole("button", { name: "Nghe thử âm báo" }).disabled).toBe(
    true,
  );
  previewNotificationPopup();
  expect(audio.createOscillator).toHaveBeenCalledTimes(2);
  expect(notificationSnapshot().items).toHaveLength(0);
  act(() => setNotificationSoundEnabled("listener", true));
  previewNotificationPopup();
  expect(audio.createOscillator).toHaveBeenCalledTimes(4);
});

it("loads a saved mute setting and keeps popups working when browser audio fails", () => {
  localStorage.setItem("planora.notification-sound.saved-muted", "false");
  expect(notificationSoundEnabled("saved-muted")).toBe(false);
  fireEvent.pointerDown(document);
  audio.createOscillator.mockImplementation(() => {
    throw new Error("Audio unavailable");
  });
  sync([]);
  expect(() => sync([notice("new")])).not.toThrow();
  expect(notificationSnapshot().popups[0].id).toBe("new");
});

it("resumes suspended audio on a gesture and cleans up on unmount", async () => {
  audio.state = "suspended";
  fireEvent.pointerDown(document);
  expect(audio.resume).toHaveBeenCalledTimes(1);
  stop();
  expect(audio.close).toHaveBeenCalledTimes(1);
  window.AudioContext.mockClear();
  fireEvent.pointerDown(document);
  expect(window.AudioContext).not.toHaveBeenCalled();
});
