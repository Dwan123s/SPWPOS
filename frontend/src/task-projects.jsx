import { Fragment, useState } from "react";
import { dateFmt, icon, modal, state, userAvatar } from "./ui.jsx";
import { TaskBatchForm } from "./task-batch.jsx";

import "./task-projects.css";

export const projectColumns = {
  progress: "Tiến độ",
  owner: "Người phụ trách",
  deadline: "Hạn chót",
};
export function openNewProject() {
  state.taskScope = "projects";
  modal(
    "Thêm project",
    "Nhập danh sách việc hoặc tải tài liệu để tạo project và các việc con.",
    <TaskBatchForm />,
  );
}
export function ProjectList({ projects, columns, filtered, onOpenProject }) {
  const [descending, setDescending] = useState(false);
  const [page, setPage] = useState(0);
  const signature = projects.map((project) => project.id).join(",");
  const [previousSignature, setPreviousSignature] = useState(signature);
  if (signature !== previousSignature) {
    setPreviousSignature(signature);
    setPage(0);
  }
  const sorted = [...projects].sort(
    (a, b) => a.name.localeCompare(b.name, "vi") * (descending ? -1 : 1),
  );
  const pages = Math.ceil(sorted.length / 12);
  const includes = (key) => columns.includes(key);
  const profile = state.workspace.profile;
  const ownerName =
    profile?.displayName ||
    `${profile?.firstName || ""} ${profile?.lastName || ""}`.trim() ||
    "Bạn";
  return (
    <div className="work-task-board project-board">
      <div className="work-task-table-scroll">
        <table className="project-list-table" aria-label="Danh sách Projects">
          <thead>
            <tr>
              <th
                scope="col"
                aria-sort={descending ? "descending" : "ascending"}
              >
                <button
                  type="button"
                  className="project-sort"
                  onClick={() => {
                    setDescending(!descending);
                    setPage(0);
                  }}
                  aria-label={
                    descending
                      ? "Sắp xếp tên project tăng dần"
                      : "Sắp xếp tên project giảm dần"
                  }
                >
                  Tên project{" "}
                  <span aria-hidden="true">{descending ? "↓" : "↑"}</span>
                </button>
              </th>
              {includes("progress") && <th scope="col">Tiến độ</th>}
              {includes("owner") && <th scope="col">Người phụ trách</th>}
              {includes("deadline") && <th scope="col">Hạn chót</th>}
              <th scope="col">
                <span className="sr-only">Phạm vi</span>
                {icon("lock")}
              </th>
            </tr>
          </thead>
          <tbody>
            {sorted.slice(page * 12, page * 12 + 12).map((project) => {
              const progress = Math.max(
                0,
                Math.min(
                  100,
                  project.status === "Completed"
                    ? 100
                    : Number(project.progress) || 0,
                ),
              );
              const overdue =
                project.status !== "Completed" &&
                project.status !== "Cancelled" &&
                Date.parse(project.deadline) <= Date.now();
              const children = state.workspace.tasks.filter(
                (task) => task.parentId === project.id && !task.isDeleted,
              );
              return (
                <Fragment key={project.id}>
                  <tr>
                    <td>
                      <div className="project-title-row">
                        <button
                          type="button"
                          className="project-name"
                          onClick={() => onOpenProject(project.id)}
                          data-id={project.id}
                          aria-label={`Mở project ${project.name}`}
                        >
                          {icon("folder")}
                          <span>
                            <strong>{project.name}</strong>
                            <small>
                              {children.length} công việc ·{" "}
                              {
                                children.filter(
                                  (task) => task.status === "Completed",
                                ).length
                              }{" "}
                              đã xong
                            </small>
                          </span>
                        </button>
                      </div>
                    </td>
                    {includes("progress") && (
                      <td>
                        <div
                          className={`project-progress${overdue ? " is-overdue" : ""}`}
                        >
                          <progress
                            max="100"
                            value={progress}
                            aria-label={`Tiến độ ${project.name}`}
                          />
                          <span>{progress}%</span>
                          {overdue && (
                            <span
                              title="Project quá hạn"
                              aria-label="Project quá hạn"
                            >
                              {icon("alertCircle")}
                            </span>
                          )}
                        </div>
                      </td>
                    )}
                    {includes("owner") && (
                      <td>
                        <span
                          className="project-owner"
                          title={ownerName}
                          aria-label={`Người phụ trách: ${ownerName}`}
                        >
                          {userAvatar(profile)}
                        </span>
                      </td>
                    )}
                    {includes("deadline") && (
                      <td>
                        <span className={overdue ? "project-overdue" : ""}>
                          {project.deadline
                            ? dateFmt(project.deadline)
                            : "Chưa có"}
                        </span>
                      </td>
                    )}
                    <td>
                      <span className="project-privacy">
                        {icon("lock")} Cá nhân
                      </span>
                    </td>
                  </tr>
                </Fragment>
              );
            })}
          </tbody>
        </table>
        {!projects.length && (
          <p className="project-empty">
            {filtered
              ? "Không có project phù hợp với bộ lọc."
              : "Chưa có project. Thêm project để nhóm các công việc liên quan."}
          </p>
        )}
      </div>
      <button
        type="button"
        className="task-quick-add-button"
        onClick={openNewProject}
        aria-haspopup="dialog"
      >
        {icon("plus")} Thêm project…
      </button>
      {pages > 1 && (
        <div className="project-pagination">
          <button
            type="button"
            disabled={page === 0}
            onClick={() => setPage(page - 1)}
          >
            Trước
          </button>
          <span>
            Trang {page + 1}/{pages}
          </span>
          <button
            type="button"
            disabled={page + 1 >= pages}
            onClick={() => setPage(page + 1)}
          >
            Sau
          </button>
        </div>
      )}
    </div>
  );
}
