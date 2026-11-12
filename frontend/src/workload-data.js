import { useEffect, useState } from "react";
import { api } from "./api.js";
import { state } from "./ui.jsx";

export function useWorkloadRequest(path, validate) {
  const [result, setResult] = useState(null);
  const [attempt, setAttempt] = useState(0);
  const account = state.workspace?.profile?.id;

  useEffect(() => {
    if (!path) return;
    let alive = true;
    let request = 0;
    let timer;
    async function refresh() {
      const current = ++request;
      clearTimeout(timer);
      try {
        const value = await api(path);
        if (!alive || current !== request) return;
        if (!validate(value))
          throw new Error("Chưa tải được dữ liệu khối lượng công việc.");
        setResult({ account, path, value, error: "" });
        const boundary = Date.parse(value.refreshAt);
        if (Number.isFinite(boundary))
          timer = setTimeout(
            () => {
              if (!document.hidden) refresh();
            },
            Math.max(1000, boundary - Date.now() + 100),
          );
      } catch (failure) {
        if (alive && current === request)
          setResult((previous) => ({
            account,
            path,
            value:
              ![401, 403].includes(failure.status) &&
              previous?.account === account &&
              previous?.path === path
                ? previous.value
                : null,
            error: failure.message,
          }));
      }
    }
    const onVisible = () => {
      if (!document.hidden) refresh();
    };
    refresh();
    window.addEventListener("workspace-updated", refresh);
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      alive = false;
      clearTimeout(timer);
      window.removeEventListener("workspace-updated", refresh);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [account, path, validate, attempt]);

  const current =
    result?.account === account && result?.path === path ? result : null;
  return {
    value: current?.value,
    error: current?.error,
    retry: () => setAttempt((n) => n + 1),
  };
}

export const isWorkloadWeekday = ({ date }) => {
  const index = new Date(date).getUTCDay();
  return index !== 0 && index !== 6;
};

export const workloadDayLabel = new Intl.DateTimeFormat("vi-VN", {
  weekday: "short",
  day: "numeric",
  month: "numeric",
  timeZone: "UTC",
});
