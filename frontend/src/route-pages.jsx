import { Component, lazy, Suspense, useLayoutEffect, useState } from "react";
import { syncDecorations, setMobileMenu } from "./shell.jsx";
import { localInput } from "./ui.jsx";

// A failed download can be retried without discarding the current session.
export function createPageResource(load, name) {
  let promise;
  const resource = {
    preload: () => (promise ??= load()),
    reset() {
      promise = undefined;
      resource.Page = lazy(() =>
        resource.preload().then((module) => ({ default: module[name] })),
      );
    },
  };
  resource.reset();
  return resource;
}
const pages = {
  teams: createPageResource(() => import("./teams.jsx"), "TeamsPage"),
  today: createPageResource(() => import("./dashboard.jsx"), "todayPage"),
  tasks: createPageResource(() => import("./page-tasks.jsx"), "tasksPage"),
  calendar: createPageResource(() => import("./schedule.jsx"), "calendarPage"),
  time: createPageResource(() => import("./page-time.jsx"), "timePage"),
  insights: createPageResource(
    () => import("./page-insights.jsx"),
    "insightsPage",
  ),
  scenario: createPageResource(
    () => import("./page-scenario.jsx"),
    "scenarioPage",
  ),
  notifications: createPageResource(
    () => import("./page-notifications.jsx"),
    "notificationsPage",
  ),
  settings: createPageResource(
    () => import("./page-settings.jsx"),
    "settingsPage",
  ),
  "admin-users": createPageResource(
    () => import("./page-admin.jsx"),
    "adminPage",
  ),
  "admin-revenue": createPageResource(
    () => import("./admin-commerce.jsx"),
    "AdminCommerce",
  ),
  "admin-system": createPageResource(
    () => import("./page-admin.jsx"),
    "adminSystemPage",
  ),
  notes: createPageResource(() => import("./notes.jsx"), "NotesPage"),
  agent: createPageResource(() => import("./agent.jsx"), "AgentPage"),
};
export function prefetchPage(route) {
  const connection = navigator.connection;
  if (connection?.saveData || /(^|-)2g$/.test(connection?.effectiveType || ""))
    return;
  pages[route]?.preload().catch(() => {});
}
export function PageSkeleton({ label = "Đang tải nội dung…" }) {
  return (
    <div className="page-skeleton" role="status" aria-label={label}>
      <span className="page-skeleton-label">
        <span className="page-loading-dot" />
        {label}
      </span>
      <div className="skeleton-metrics" aria-hidden="true">
        {[0, 1, 2].map((i) => (
          <div key={i}>
            <i />
            <b />
          </div>
        ))}
      </div>
      <div className="skeleton-panel" aria-hidden="true">
        {[0, 1, 2, 3].map((i) => (
          <span key={i} />
        ))}
      </div>
    </div>
  );
}
class PageError extends Component {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <section className="page-load-error" role="alert">
        <h2>Chưa mở được trang này</h2>
        <p>Kết nối có thể bị gián đoạn. Bạn có thể thử lại ngay tại đây.</p>
        <button
          type="button"
          className="btn secondary"
          onClick={this.props.retry}
        >
          Thử tải lại
        </button>
      </section>
    );
  }
}
function ReadyPage({ resource, route, onReady, ...props }) {
  const Page = resource.Page;
  // Runs only after the suspended child has committed, never on the skeleton.
  useLayoutEffect(() => {
    syncDecorations();
    setMobileMenu(false);
    if (route === "scenario") {
      const input = document.querySelector('input[name="deadline"]');
      if (input && !input.value)
        input.value = localInput(Date.now() + 7 * 86400000);
    }
    onReady?.();
  }, [route, onReady]);
  return <Page {...props} />;
}
export function RoutePage({ route, ...props }) {
  return (
    <ResourcePage
      key={route}
      resource={pages[route]}
      route={route}
      {...props}
    />
  );
}
export function ResourcePage({ resource, route, ...props }) {
  const [attempt, setAttempt] = useState(0);
  return (
    <PageError
      key={`${route}-${attempt}`}
      retry={() => {
        resource.reset();
        setAttempt((x) => x + 1);
      }}
    >
      <Suspense fallback={<PageSkeleton />}>
        <ReadyPage resource={resource} route={route} {...props} />
      </Suspense>
    </PageError>
  );
}
