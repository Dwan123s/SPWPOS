import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, dateFmt, timeFmt, icon, minutes, toast, toUtc, zone } from "./ui.jsx";
import { askNora } from "./agent-handoff.js";
import { TeamChat } from "./team-chat.jsx";
import { TeamRenewalNotice } from "./team-renewal-notice.jsx";
import {
  MyJoinRequests,
  TeamJoinRequests,
  RemoveTeamMemberDialog,
} from "./team-membership.jsx";
import "./team-membership.css";
import { TeamInviteDialog, ReceivedTeamInvitations } from "./team-friend-invitations.jsx";
import {
  CompletionFileUpload,
  CompletionReport,
} from "./team-completion-files.jsx";
import {
  captureTeamInvite,
  clearTeamInvite,
  parseTeamInvite,
} from "./team-invite-link.js";
import { SessionList } from "./team-session-list.jsx";
import { TeamScheduleCompare } from "./team-schedule-compare.jsx";
import { TeamChecklist, TeamTaskSplitDialog } from "./team-task-checklist.jsx";
import { TaskRequirements, TaskSourceFiles } from "./team-task-notes.jsx";
export { SessionList } from "./team-session-list.jsx";
import {
  GroupHub,
  TeamHeader,
  TeamOverview,
  TeamMembers,
  TaskBoard,
  TeamTaskComposer,
  TeamDialog,
  TeamActivity,
} from "./team-components.jsx";
import "./team-workspace.css";
import "./team-overview.css";
import "./team-task-board.css";
import "./team-task-detail.css";
import "./team-recipient-select.css";
import { finishTeamTask, pendingTeamTask } from "./team-task-link.js";
import {
  pendingTeamJoinReview,
  finishTeamJoinReview,
} from "./team-notification-link.js";

export function PrivateClaimPreview({
  claim,
  busy,
  onConfirm,
  onCancel,
  onDiscuss,
  onAdjust,
}) {
  const proposalId = claim.options.length && state.noraAvailable ? claim.id : null;
  const [review, setReview] = useState(null);
  useEffect(() => {
    if (!proposalId) return;
    const request = new AbortController();
    api(`/api/teams/claims/${proposalId}/explanation`, "POST", undefined, true, request.signal)
      .then((result) => {
        if (!request.signal.aborted) setReview({ proposalId, text: result?.explanation });
      })
      .catch(() => {
        if (!request.signal.aborted) setReview({ proposalId });
      });
    return () => request.abort();
  }, [proposalId]);
  const currentReview = review?.proposalId === proposalId ? review : null;

  return (
    <section className="team-claim-content" aria-label="Phương án riêng tư">
      <div className="team-claim-privacy">
        {icon("shield")}
        <p>
          Phương án chỉ dành cho bạn. Nhóm chỉ thấy người nhận và tiến độ đã xác
          nhận.
        </p>
      </div>
      {claim.analysis && (
        <div className="team-claim-analysis">
          <span>{icon("spark")}</span>
          <div>
            <h3>Kết quả xếp lịch</h3>
            <p>{claim.analysis}</p>
          </div>
        </div>
      )}
      {claim.warnings.map((w, i) => (
        <p role="status" className="team-error" key={i}>
          {w}
        </p>
      ))}
      {proposalId && !currentReview && <p role="status">NORA đang chuẩn bị giải thích…</p>}
      {currentReview?.text && (
        <div className="team-claim-analysis" role="status">
          <span>{icon("spark")}</span>
          <div>
            <h3>Giải thích của NORA</h3>
            <p>{currentReview.text}</p>
          </div>
        </div>
      )}
      {claim.failure && (
        <section className="team-claim-failure">
          <h3>
            {claim.failure.needsAvailability
              ? "Còn một bước: xác nhận giờ có thể làm việc"
              : "Vì sao chưa xếp được task?"}
          </h3>
          {!claim.failure.needsAvailability && (
            <div className="team-failure-metrics">
              <div>
                <span>Công việc cần</span>
                <strong>{minutes(claim.failure.requiredMinutes, "long")}</strong>
              </div>
              <div>
                <span>Quỹ có thể xếp trước hạn</span>
                <strong>{minutes(claim.failure.capacityMinutes, "long")}</strong>
              </div>
              {claim.failure.missingMinutes > 0 && (
                <div>
                  <span>Còn thiếu ít nhất</span>
                  <strong>{minutes(claim.failure.missingMinutes, "long")}</strong>
                </div>
              )}
            </div>
          )}
          <ul>
            {claim.failure.blockers.map((reason, i) => (
              <li key={i}>{reason}</li>
            ))}
          </ul>
          {!claim.failure.needsAvailability && (
            <div className="team-actions">
              {onDiscuss && (
                <button
                  className="btn primary"
                  disabled={busy}
                  onClick={onDiscuss}
                >
                  Nhờ NORA phân tích điều kiện
                </button>
              )}
              {onAdjust && (
                <button
                  className="btn secondary"
                  disabled={busy}
                  onClick={onAdjust}
                >
                  Điều chỉnh yêu cầu task
                </button>
              )}
            </div>
          )}
        </section>
      )}
      {claim.options.map((o, i) => (
        <section
          className="team-claim-option team-claim-option-readable"
          key={o.index}
        >
          <div className="team-option-heading">
            <div>
              <span className="team-eyebrow">
                PHƯƠNG ÁN {String(i + 1).padStart(2, "0")}
              </span>
              <h3>{o.label}</h3>
            </div>
            <span className="team-status-pill">
              {icon("check")} Đã kiểm tra lịch
            </span>
          </div>
          <TeamScheduleCompare before={o.before} after={o.after} />
          {!!o.explanations.length && (
            <details className="team-plan-reasons">
              <summary>Vì sao NORA chọn phương án này?</summary>
              {o.explanations.map((t, n) => (
                <p key={n}>{t}</p>
              ))}
            </details>
          )}
          <button
            className="btn primary"
            disabled={busy}
            onClick={() => onConfirm(o.index)}
          >
            {icon("check")} {claim.canUndo === false ? "Xác nhận đổi lịch" : "Xác nhận nhận việc"} · Phương án {o.index + 1}
          </button>
        </section>
      ))}
      <button className="btn secondary" disabled={busy} onClick={onCancel}>
        Hủy xem phương án
      </button>
    </section>
  );
}
export function TeamsPage({ onWorkspaceChanged }) {
  const [taskToOpen, setTaskToOpen] = useState(null);
  const [attentionToOpen, setAttentionToOpen] = useState(null);
  const [groupView, setGroupView] = useState("work");
  const now = Date.now();
  const [teams, setTeams] = useState([]),
    [detail, setDetail] = useState(null),
    [loadingTeams, setLoadingTeams] = useState(true);
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [setupOpen, setSetupOpen] = useState(false);
  const [taskDetailOpen, setTaskDetailOpen] = useState(false);
  const [invite, setInvite] = useState(false);
  const [composerOpen, setComposerOpen] = useState(false),
    [text, setText] = useState(""),
    [changeTask, setChangeTask] = useState(null),
    [draft, setDraft] = useState(null);
  const [briefFile, setBriefFile] = useState(null);
  const [splitBrief, setSplitBrief] = useState(false);
  const [briefTaskCount, setBriefTaskCount] = useState(null);
  const [claim, setClaim] = useState(null),
    [claimOpen, setClaimOpen] = useState(false),
    [claimRequest, setClaimRequest] = useState(null),
    [undo, setUndo] = useState(null),
    [returning, setReturning] = useState(null);
  const uid = state.workspace?.profile?.id;
  const [completion, setCompletion] = useState(null);
  const [completionPolicyDraft, setCompletionPolicyDraft] = useState(null);
  const [splittingTask, setSplittingTask] = useState(null);
  const [extendingTask, setExtendingTask] = useState(null);
  const [reviewNote, setReviewNote] = useState("");
  const [completionFiles, setCompletionFiles] = useState([]);
  const [completionNote, setCompletionNote] = useState("");
  const [completionReportUrl, setCompletionReportUrl] = useState("");
  const [readingCompletionFiles, setReadingCompletionFiles] = useState(false);
  const [joinLink, setJoinLink] = useState(() => captureTeamInvite());
  const [invitePreview, setInvitePreview] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [joinRequests, setJoinRequests] = useState([]);
  const [joinMessage, setJoinMessage] = useState("");
  const [removingMember, setRemovingMember] = useState(null);
  useEffect(() => {
    if (detail?.renewal?.status !== "Suspended") return;
    setClaim(null);
    setClaimOpen(false);
    setComposerOpen(false);
    setRemovingMember(null);
    setCompletion(null);
    setCompletionPolicyDraft(null);
    setSplittingTask(null);
    setExtendingTask(null);
    setReturning(null);
    setInvite(null);
    setDraft(null);
    setTaskDetailOpen(false);
  }, [detail?.renewal?.status]);
  const joinReviewRef = useRef(null);
  const [reviewTeamId, setReviewTeamId] = useState(null);
  useEffect(() => {
    let live = true;
    const open = async () => {
      const request = pendingTeamJoinReview(uid);
      if (!request) return;
      setError("");
      try {
        const next = await api(
          `/api/teams/${encodeURIComponent(request.teamId)}`,
        );
        if (!live || pendingTeamJoinReview(uid) !== request) return;
        setDetail(next);
        setGroupView("members");
        setReviewTeamId(next.team.id);
        finishTeamJoinReview(request);
      } catch (failure) {
        if (live && pendingTeamJoinReview(uid) === request) {
          setError(failure.message);
          finishTeamJoinReview(request);
        }
      }
    };
    open();
    window.addEventListener("team-join-review", open);
    return () => {
      live = false;
      window.removeEventListener("team-join-review", open);
    };
  }, [uid]);
  useEffect(() => {
    if (groupView !== "members" || reviewTeamId !== detail?.team.id || !joinReviewRef.current) return;
    joinReviewRef.current.focus({ preventScroll: true });
    joinReviewRef.current.scrollIntoView?.({
      behavior: "smooth",
      block: "center",
    });
    setReviewTeamId(null);
  }, [groupView, reviewTeamId, detail?.team.id]);
  useEffect(() => {
    if (!joinLink?.code) return;
    let live = true;
    setPreviewLoading(true);
    setError("");
    api(`/api/teams/invites/${joinLink.code}`)
      .then((data) => {
        if (live) setInvitePreview(data);
      })
      .catch((e) => {
        if (live) setError(e.message);
      })
      .finally(() => {
        if (live) setPreviewLoading(false);
      });
    return () => {
      live = false;
    };
  }, [joinLink?.code]);
  const closeJoinLink = () => {
    clearTeamInvite();
    setJoinLink(null);
    setInvitePreview(null);
    setError("");
  };
  const selectedTeamId = detail?.team.id;
  useEffect(() => {
    let live = true;
    const open = async () => {
      const requested = pendingTeamTask(uid);
      if (!requested) return;
      setError("");
      try {
        const next = await api(`/api/teams/${requested.teamId}`);
        if (!live || pendingTeamTask(uid) !== requested) return;
        setDetail(next);
        setGroupView("work");
        if (requested.taskId && !next.tasks.some((task) => task.id === requested.taskId)) {
          setTaskToOpen(null);
          setError("Công việc này không còn trong bảng nhóm.");
        } else {
          setTaskToOpen(requested.taskId ? requested : null);
        }
        finishTeamTask(requested);
      } catch (failure) {
        if (live && pendingTeamTask(uid) === requested) {
          setError(failure.message);
          finishTeamTask(requested);
        }
      }
    };
    open();
    window.addEventListener("open-team-task", open);
    return () => {
      live = false;
      window.removeEventListener("open-team-task", open);
    };
  }, [uid]);
  useEffect(() => {
    if (!selectedTeamId) return;
    let live = true;
    const timer = setInterval(async () => {
      if (document.hidden || busy) return;
      try {
        const next = await api(`/api/teams/${selectedTeamId}`);
        if (live) setDetail(next);
      } catch (failure) {
        if (live && failure.code === "TEAM_FORBIDDEN") {
          setDetail(null);
          setClaim(null);
          setClaimOpen(false);
          setComposerOpen(false);
          setRemovingMember(null);
          setCompletion(null);
          setCompletionPolicyDraft(null);
          setSplittingTask(null);
          setReturning(null);
          setInvite(null);
          setDraft(null);
          setUndo(null);
          setError("Bạn không còn là thành viên của nhóm này.");
          try {
            const list = await api("/api/teams");
            if (live) setTeams(list);
          } catch {
            /* Group data is already cleared even if the list refresh fails. */
          }
        }
      }
    }, 7000);
    return () => {
      live = false;
      clearInterval(timer);
    };
  }, [selectedTeamId, busy]);
  useEffect(() => {
    let live = true;
    async function refreshRequests() {
      if (document.hidden || busy) return;
      try {
        const [requests, list] = await Promise.all([
          api("/api/teams/join-requests"),
          api("/api/teams"),
        ]);
        if (live) {
          setJoinRequests(Array.isArray(requests) ? requests : []);
          setTeams(list);
        }
      } catch {
        /* Explicit refresh and actions surface errors. */
      }
    }
    refreshRequests();
    const timer = setInterval(refreshRequests, 7000);
    return () => {
      live = false;
      clearInterval(timer);
    };
  }, [busy]);
  useEffect(() => {
    let live = true;
    api("/api/teams")
      .then((data) => {
        if (live) setTeams(data);
      })
      .catch((e) => {
        if (live) setError(e.message);
      })
      .finally(() => {
        if (live) setLoadingTeams(false);
      });
    return () => {
      live = false;
    };
  }, []);
  async function run(action) {
    if (busy) return false;
    setBusy(true);
    setError("");
    try {
      await action();
      return true;
    } catch (e) {
      setError(e.message);
      return false;
    } finally {
      setBusy(false);
    }
  }
  async function post(path, body) {
    return api(`/api/teams/${path}`, "POST", body);
  }
  async function reload(id = detail?.team.id) {
    const list = await api("/api/teams");
    setTeams(list);
    const requests = await api("/api/teams/join-requests");
    setJoinRequests(Array.isArray(requests) ? requests : []);
    if (id && list.some((t) => t.id === id))
      setDetail(await api(`/api/teams/${id}`));
    else setDetail(null);
  }
  async function requestJoin(code) {
    const result = await post("join", { text: parseTeamInvite(code) });
    setJoinMessage(
      result.status === "Pending"
        ? "Đã gửi yêu cầu. Bạn sẽ vào nhóm sau khi trưởng nhóm duyệt."
        : "",
    );
    await reload(result.status === "Pending" ? undefined : result.id);
  }
  async function changed() {
    await reload();
    await onWorkspaceChanged?.();
  }
  function openComposer(task = null) {
    if (detail?.team.role !== "Leader") return;
    setChangeTask(task);
    setText("");
    setDraft(null);
    setBriefFile(null);
    setSplitBrief(false);
    setBriefTaskCount(null);
    setError("");
    setComposerOpen(true);
  }
  function openClaim(task, change = false) {
    return run(async () => {
      setClaimOpen(true);
      setClaimRequest({ task, change });
      setClaim(null);
      setUndo(null);
      setClaim(
        await post(
          `tasks/${task.id}/${change ? "changes/preview" : "preview"}`,
        ),
      );
    });
  }
  function cancelClaim() {
    return run(async () => {
      try {
        if (claim?.options.length) await post(`claims/${claim.id}/cancel`);
      } finally {
        setClaim(null);
        setClaimOpen(false);
      }
    });
  }
  const modalOpen =
    taskDetailOpen ||
    setupOpen ||
    claimOpen ||
    composerOpen ||
    !!splittingTask ||
    !!extendingTask ||
    !!invite ||
    !!returning ||
    !!completion ||
    !!completionPolicyDraft ||
    !!removingMember ||
    !!joinLink;
  const ready =
    !!state.workspace?.availabilityPolicy?.confirmed ||
    !!state.workspace?.availability?.length ||
    !!state.workspace?.exceptions?.some((x) => x.isAvailable);
  return (
    <div className="team-hub">
      <GroupHub
        teams={teams}
        loading={loadingTeams}
        onDialogChange={setSetupOpen}
        selected={detail?.team.id}
        busy={busy}
        error={error}
        onBack={() => {
          setDetail(null);
          setGroupView("work");
          setTaskToOpen(null);
          setAttentionToOpen(null);
          setError("");
        }}
        onSelect={(id) =>
          run(async () => {
            setDetail(await api(`/api/teams/${id}`));
            setGroupView("work");
            setClaim(null);
            setInvite(null);
            setDraft(null);
          })
        }
        onCreate={(name) =>
          run(async () => {
            const result = await post("", { name });
            await reload(result.id);
          })
        }
        onJoin={(code) =>
          run(async () => {
            await requestJoin(code);
          })
        }
        onCreateLink={(id) =>
          run(async () => {
            await reload(id);
            setInvite(true);
          })
        }
      />
      {joinMessage && (
        <p className="team-success" role="status">
          {joinMessage}
        </p>
      )}
      <ReceivedTeamInvitations onAccepted={async (id) => {
        await reload(id);
        setGroupView("work");
        setJoinMessage("Bạn đã tham gia nhóm. Công việc và trao đổi của nhóm đã sẵn sàng.");
      }} />
      <MyJoinRequests requests={joinRequests} />
      {error && !modalOpen && (
        <p className="team-error" role="alert">
          {icon("alertCircle")}
          {error}
        </p>
      )}
      {undo && (
        <section className="team-success" role="status">
          <span>{icon("check")} Đã nhận task và cập nhật lịch cá nhân.</span>
          <button
            className="btn secondary"
            disabled={busy}
            onClick={() =>
              run(async () => {
                await post(`claims/${undo}/undo`);
                setUndo(null);
                await changed();
              })
            }
          >
            Hoàn tác nhận việc
          </button>
        </section>
      )}
      {detail && (
        <div className="team-workspace">
          {detail.renewal && <TeamRenewalNotice key={`${detail.team.id}:${detail.renewal.expiredAt}:${detail.renewal.status}`} detail={detail} />}
          {detail.renewal?.status === "Suspended" ? <section className="team-panel"><h2>{detail.team.name}</h2><button type="button" className="btn secondary" disabled={busy} onClick={() => run(() => reload())}>Kiểm tra gia hạn</button></section> : <>
          <TeamHeader
            detail={detail}
            busy={busy}
            onInvite={() =>
              run(async () => {
                setInvite(true);
              })
            }
            onRefresh={() => run(() => reload())}
            onCompletionPolicy={() => {
              setError("");
              setCompletionPolicyDraft(detail.completionPolicy);
            }}
            onCompose={detail.tasks.length || groupView !== "work" ? () => openComposer() : undefined}
          />
          <nav className="team-workspace-nav" aria-label="Mục trong nhóm">
            {[
              { id: "work", label: "Công việc", glyph: "tasks" },
              { id: "members", label: "Thành viên", glyph: "users" },
              { id: "chat", label: "Trao đổi", glyph: "chat" },
              { id: "overview", label: "Tổng quan", glyph: "insights" },
            ].map(({ id, label, glyph }) => (
              <button key={id} type="button" aria-pressed={groupView === id} onClick={() => setGroupView(id)}>
                {icon(glyph)} {label}
                {id === "members" && detail.team.role === "Leader" && detail.joinRequests?.length > 0 && (
                  <span className="team-count" aria-label={`${detail.joinRequests.length} yêu cầu tham gia`}>{detail.joinRequests.length}</span>
                )}
              </button>
            ))}
          </nav>
          <div className="team-workspace-pane" hidden={groupView !== "work"}>
              <TaskBoard
                key={detail.team.id}
                detail={detail}
                now={now}
                attentionToOpen={attentionToOpen?.teamId === detail.team.id ? attentionToOpen : null}
                taskToOpen={
                  taskToOpen?.teamId === detail.team.id ? taskToOpen : null
                }
                onTaskOpened={() => setTaskToOpen(null)}
                onImportanceChanged={() => run(() => reload())}
                error={error}
                onDialogChange={setTaskDetailOpen}
                onExecutionChanged={changed}
                onRecipientChange={(task, userId) =>
                  run(async () => {
                    await post(`tasks/${task.id}/recipient`, {
                      version: task.version,
                      userId,
                    });
                    await reload();
                  })
                }
                onRequestAvailability={(task, userId) => run(async () => {
                  await post(`tasks/${task.id}/availability/request`, { version: task.version, userId });
                  await reload();
                })}
                onAttachSource={(task, file) =>
                  run(async () => {
                    await post(`tasks/${task.id}/source`, {
                      version: task.version,
                      requestId: file.requestId,
                      file: {
                        name: file.name,
                        mimeType: file.mimeType,
                        data: file.data,
                      },
                    });
                    await reload();
                  })
                }
                uid={uid}
                busy={busy}
                onCompose={() => openComposer()}
                onClaim={openClaim}
                onExtendDeadline={(task) => {
                  setError("");
                  setExtendingTask(task);
                }}
                onSplit={(task) => {
                  setError("");
                  setSplittingTask(task);
                }}
                onChecklistToggle={(task, itemId, completed) => run(async () => {
                  await api(`/api/teams/tasks/${task.id}/checklist/${itemId}`, "PUT", {
                    version: task.version,
                    completed,
                  });
                  await changed();
                })}
                onProgress={(task, progress) => {
                  if (progress === 100) {
                    setError("");
                    setCompletionFiles([]);
                    setCompletionNote("");
                    setCompletionReportUrl("");
                    setReadingCompletionFiles(false);
                    setCompletion({
                      task,
                      mode: "submit",
                      requestId: crypto.randomUUID(),
                    });
                    return;
                  }
                  return run(async () => {
                    await post(`tasks/${task.id}/progress`, {
                      version: task.version,
                      progress,
                    });
                    await changed();
                  });
                }}
                onReviewCompletion={(task) => {
                  setError("");
                  setReviewNote("");
                  setCompletion({ task, mode: "review" });
                }}
                onReturn={(task) =>
                  run(async () =>
                    setReturning(await post(`tasks/${task.id}/return/preview`)),
                  )
                }
                onChange={openComposer}
                onReject={(task) =>
                  run(async () => {
                    await post(`tasks/${task.id}/changes/reject`, {
                      version: task.version,
                    });
                    await reload();
                  })
                }
              />
              <div className={`team-workspace-readiness${ready ? " ready" : ""}`}>
                {icon(ready ? "shield" : "calendar")}
                {ready ? "Lịch cá nhân đã sẵn sàng" : "Bạn cần đặt giờ làm trước khi nhận việc."}
                {!ready && (
                  <button type="button" className="team-text-button" onClick={() => askNora(
                    "Tôi muốn gửi thời gian biểu và xác nhận giờ có thể làm việc để nhận task nhóm.",
                  )}>
                    Thiết lập với NORA {icon("arrow")}
                  </button>
                )}
              </div>
          </div>
          {groupView === "members" && (
            <div className="team-workspace-pane team-workspace-members">
              <TeamJoinRequests
                detail={detail}
                sectionRef={joinReviewRef}
                busy={busy}
                onReview={(request, approved) =>
                  run(async () => {
                    await post(
                      `${detail.team.id}/join-requests/${request.userId}/review`,
                      { version: request.version, approved },
                    );
                    await changed();
                  })
                }
              />
              <TeamMembers detail={detail} uid={uid} busy={busy} onRemove={(member) => {
                setError("");
                setRemovingMember(member);
              }} />
            </div>
          )}
          <div className="team-workspace-pane" hidden={groupView !== "chat"}>
            <TeamChat key={detail.team.id} teamId={detail.team.id} name={detail.team.name} active={groupView === "chat"} />
          </div>
          {groupView === "overview" && (
            <div className="team-workspace-pane">
              <TeamOverview
                tasks={detail.tasks}
                busy={busy}
                now={now}
                onOpenAttention={detail.team.role === "Leader" ? (id) => {
                  setGroupView("work");
                  setAttentionToOpen({ teamId: detail.team.id, id });
                } : undefined}
                onOpenTask={(taskId) => {
                  setGroupView("work");
                  setTaskToOpen({ teamId: detail.team.id, taskId });
                }}
              />
              <TeamActivity detail={detail} />
            </div>
          )}
          </>}
        </div>
      )}
      {!detail && (
        <section className="team-privacy-strip">
          <span>{icon("lock")}</span>
          <div>
            <h3>Chung deadline, riêng lịch thực hiện.</h3>
            <p>
              Cùng một hạn chót, mỗi thành viên vẫn có thể nhận lịch làm khác
              nhau. NORA chỉ dựa trên thời gian biểu của chính người nhận.
            </p>
          </div>
          <button
            className="btn secondary"
            onClick={() =>
              askNora(
                "Tôi muốn gửi thời gian biểu và xác nhận giờ có thể làm việc để nhận task nhóm.",
              )
            }
          >
            Chuẩn bị lịch cá nhân {icon("arrow")}
          </button>
        </section>
      )}
      {completionPolicyDraft && (
        <TeamDialog
          title="Quy định gửi duyệt"
          subtitle={detail.team.name}
          busy={busy}
          onClose={() => setCompletionPolicyDraft(null)}
        >
          <form
            className="team-completion-dialog"
            onSubmit={(event) => {
              event.preventDefault();
              run(async () => {
                try {
                  const policy = await api(
                    `/api/teams/${detail.team.id}/completion-policy`,
                    "PUT",
                    completionPolicyDraft,
                  );
                  setDetail((current) => ({ ...current, completionPolicy: policy }));
                  setCompletionPolicyDraft(null);
                } catch (failure) {
                  if (failure.status === 409) {
                    const latest = await api(`/api/teams/${detail.team.id}`);
                    setDetail(latest);
                    setCompletionPolicyDraft(latest.team.role === "Leader" ? latest.completionPolicy : null);
                  }
                  throw failure;
                }
              });
            }}
          >
            <label className="team-policy-option">
              <input
                type="checkbox"
                disabled={busy}
                checked={completionPolicyDraft.requireReportUrl}
                onChange={(event) => setCompletionPolicyDraft({ ...completionPolicyDraft, requireReportUrl: event.target.checked })}
              />
              Bắt buộc có link báo cáo khi gửi duyệt
            </label>
            <label className="team-policy-option">
              <input
                type="checkbox"
                disabled={busy}
                checked={completionPolicyDraft.notifyLeader}
                onChange={(event) => setCompletionPolicyDraft({ ...completionPolicyDraft, notifyLeader: event.target.checked })}
              />
              Thông báo cho trưởng nhóm khi có báo cáo mới
            </label>
            {error && <p role="alert" className="team-error">{error}</p>}
            <div className="team-actions">
              <button type="submit" className="btn primary" disabled={busy}>Lưu quy định</button>
              <button type="button" className="btn secondary" disabled={busy} onClick={() => setCompletionPolicyDraft(null)}>Hủy</button>
            </div>
          </form>
        </TeamDialog>
      )}
      {completion && (
        <TeamDialog
          className={completion.mode === "review" ? "team-review-drawer" : ""}
          title={
            completion.mode === "submit"
              ? "Gửi xác nhận hoàn thành"
              : "Duyệt hoàn thành task"
          }
          subtitle={completion.task.name}
          busy={busy || readingCompletionFiles}
          onClose={() => setCompletion(null)}
        >
          <div className="team-completion-dialog">
            <p>
              {completion.mode === "submit"
                ? "Bạn xác nhận đã làm xong công việc này? Task sẽ chuyển sang Chờ duyệt. Trưởng nhóm kiểm tra và xác nhận lần cuối trước khi hệ thống thông báo hoàn thành cho nhóm."
                : `${detail.members.find((m) => m.userId === completion.task.assigneeId)?.name || "Thành viên"} đã báo hoàn thành. Hãy kiểm tra kết quả công việc trước khi xác nhận.`}
            </p>
            {completion.mode === "submit" && (
              <>
                <label>
                  {detail.completionPolicy?.requireReportUrl ? "Link báo cáo (bắt buộc)" : "Link báo cáo (không bắt buộc)"}
                  <input
                    type="url"
                    placeholder="https://…"
                    maxLength={2048}
                    required={detail.completionPolicy?.requireReportUrl === true}
                    value={completionReportUrl}
                    disabled={busy || readingCompletionFiles}
                    onChange={(event) => {
                      setCompletionReportUrl(event.target.value);
                      setCompletion((c) => ({ ...c, requestId: crypto.randomUUID() }));
                    }}
                  />
                </label>
                <label>
                  Ghi chú kết quả (không bắt buộc)
                  <textarea
                    rows={3}
                    maxLength={1000}
                    value={completionNote}
                    disabled={busy || readingCompletionFiles}
                    placeholder="Tóm tắt kết quả đã làm hoặc hướng dẫn kiểm tra file…"
                    onChange={(e) => {
                      setCompletionNote(e.target.value);
                      setCompletion((c) => ({
                        ...c,
                        requestId: crypto.randomUUID(),
                      }));
                    }}
                  />
                </label>
                <CompletionFileUpload
                  files={completionFiles}
                  busy={busy}
                  onReadingChange={setReadingCompletionFiles}
                  onChange={(files) => {
                    setCompletionFiles(files);
                    setCompletion((c) => ({
                      ...c,
                      requestId: crypto.randomUUID(),
                    }));
                  }}
                />
              </>
            )}
            {completion.mode === "review" && (
              <>
                <TaskRequirements task={completion.task} />
                {completion.task.sourceFiles?.length > 0 && <TaskSourceFiles task={completion.task} />}
                <CompletionReport task={completion.task} />
              </>
            )}
            {completion.mode === "review" && (
              <label>
                Nhận xét cho thành viên (không bắt buộc)
                <textarea
                  rows={3}
                  maxLength={500}
                  value={reviewNote}
                  onChange={(e) => setReviewNote(e.target.value)}
                  placeholder="Ghi nhận kết quả hoặc nội dung cần bổ sung…"
                />
              </label>
            )}
            {error && (
              <p role="alert" className="team-error">
                {error}
              </p>
            )}
            <div className="team-actions">
              <button
                className="btn primary"
                disabled={
                  busy || readingCompletionFiles || (completion.mode === "submit" &&
                    detail.completionPolicy?.requireReportUrl && !completionReportUrl.trim())
                }
                onClick={() =>
                  run(async () => {
                    if (completion.mode === "submit")
                      await post(
                        `tasks/${completion.task.id}/completion/submit`,
                        {
                          version: completion.task.version,
                          requestId: completion.requestId,
                          note: completionNote,
                          ...(completionReportUrl.trim() && { reportUrl: completionReportUrl.trim() }),
                          files: completionFiles.map((file) => ({
                            name: file.name,
                            data: file.data,
                          })),
                        },
                      );
                    else
                      await post(
                        `tasks/${completion.task.id}/completion/review`,
                        {
                          version: completion.task.version,
                          requestId: completion.task.completionRequestId,
                          approved: true,
                          note: reviewNote,
                        },
                      );
                    await changed();
                    setCompletion(null);
                  })
                }
              >
                {completion.mode === "submit"
                  ? "Xác nhận & gửi duyệt"
                  : "Duyệt hoàn thành"}
              </button>
              {completion.mode === "review" && (
                <button
                  className="btn secondary"
                  disabled={busy}
                  onClick={() =>
                    run(async () => {
                      await post(
                        `tasks/${completion.task.id}/completion/review`,
                        {
                          version: completion.task.version,
                          requestId: completion.task.completionRequestId,
                          approved: false,
                          note: reviewNote,
                        },
                      );
                      await changed();
                      setCompletion(null);
                    })
                  }
                >
                  Yêu cầu bổ sung
                </button>
              )}
              <button
                className="btn secondary"
                disabled={busy || readingCompletionFiles}
                onClick={() => setCompletion(null)}
              >
                Để sau
              </button>
            </div>
          </div>
        </TeamDialog>
      )}
      {extendingTask && (
        <TeamDialog title="Gia hạn công việc" subtitle={extendingTask.name} busy={busy}
          onClose={() => setExtendingTask(null)}>
          <form className="team-form" onSubmit={(event) => {
            event.preventDefault();
            const value = new FormData(event.currentTarget).get("deadline");
            void run(async () => {
              await api(`/api/teams/tasks/${extendingTask.id}/deadline`, "PUT", {
                version: extendingTask.version,
                deadline: toUtc(value),
              });
              setExtendingTask(null);
              toast("Đã gia hạn công việc.");
              await reload();
            });
          }}>
            <p>Hạn cũ {dateFmt(extendingTask.deadline, { year: "numeric" })} · {timeFmt(extendingTask.deadline)}. Thời lượng dự kiến {minutes(extendingTask.estimatedMinutes, "long")}.</p>
            <label>Hạn chót mới ({zone()})
              <input type="datetime-local" name="deadline" required disabled={busy} />
            </label>
            {error && <p className="team-error" role="alert">{error}</p>}
            <div className="team-actions">
              <button type="submit" className="btn primary" disabled={busy}>{busy ? "Đang lưu…" : "Lưu hạn mới"}</button>
              <button type="button" className="btn secondary" disabled={busy} onClick={() => setExtendingTask(null)}>Hủy</button>
            </div>
          </form>
        </TeamDialog>
      )}
      {composerOpen && (
        <TeamTaskComposer
          text={text}
          setText={setText}
          changeTask={changeTask}
          draft={draft}
          setDraft={setDraft}
          file={briefFile}
          setFile={setBriefFile}
          splitBrief={splitBrief}
          setSplitBrief={setSplitBrief}
          memberCount={detail.members.length}
          taskCount={Math.min(
            briefTaskCount ?? detail.members.length,
            detail.members.length,
          )}
          setTaskCount={(count) => {
            setBriefTaskCount(count);
            setDraft(null);
          }}
          members={detail.members}
          busy={busy}
          error={error}
          onEditDraft={
            draft?.tasks
              ? (edits) =>
                  run(async () =>
                    setDraft(
                      await post(
                        `${detail.team.id}/briefs/${draft.proposalId}/edit`,
                        {
                          revision:
                            draft.revision ||
                            "00000000-0000-0000-0000-000000000000",
                          tasks: edits,
                        },
                      ),
                    ),
                  )
              : undefined
          }
          onClose={() => setComposerOpen(false)}
          onManualDraft={(input) =>
            run(async () => {
              setDraft(null);
              const draft = changeTask
                ? await post(`tasks/${changeTask.id}/changes/manual`, {
                    version: changeTask.version,
                    task: input,
                  })
                : await post(`${detail.team.id}/drafts/manual`, input);
              setDraft(draft);
            })
          }
          onDraft={() =>
            run(async () =>
              setDraft(
                await post(
                  !changeTask && splitBrief
                    ? `${detail.team.id}/briefs`
                    : changeTask
                      ? `tasks/${changeTask.id}/changes`
                      : `${detail.team.id}/drafts`,
                  !changeTask && splitBrief
                    ? {
                        text,
                        taskCount: Math.min(
                          briefTaskCount ?? detail.members.length,
                          detail.members.length,
                        ),
                        file: briefFile
                          ? {
                              name: briefFile.name,
                              mimeType: briefFile.mimeType,
                              data: briefFile.data,
                            }
                          : null,
                      }
                    : { text },
                ),
              ),
            )
          }
          onConfirm={(recipients = {}) =>
            run(async () => {
              const selections = (draft.tasks || [draft.task])
                .filter((task) => recipients[task.id])
                .map((task) => ({
                  taskId: task.id,
                  userId: recipients[task.id],
                }));
              await post(
                `${detail.team.id}/${draft.tasks ? "briefs" : "drafts"}/${draft.proposalId}/confirm`,
                draft.tasks &&
                  draft.revision &&
                  draft.revision !== "00000000-0000-0000-0000-000000000000"
                  ? { recipients: selections, revision: draft.revision }
                  : changeTask || !selections.length
                    ? undefined
                    : draft.tasks
                      ? { recipients: selections }
                      : { userId: selections[0].userId },
              );
              setComposerOpen(false);
              setDraft(null);
              setText("");
              setChangeTask(null);
              setBriefFile(null);
              setSplitBrief(false);
              await reload();
            })
          }
        />
      )}
      {joinLink && (
        <TeamDialog
          title="Lời mời tham gia nhóm"
          subtitle="Kết nối với nhóm qua link mời"
          busy={busy}
          onClose={closeJoinLink}
        >
          <div className="team-invite-content">
            <span className="team-tile-icon">{icon("users")}</span>
            {previewLoading ? (
              <p role="status">Đang kiểm tra lời mời…</p>
            ) : invitePreview ? (
              <>
                <h3>{invitePreview.teamName}</h3>
                <p>
                  {invitePreview.memberCount} thành viên · Hết hạn{" "}
                  {dateFmt(invitePreview.expiresAt)}
                </p>
                <p>
                  {invitePreview.alreadyMember
                    ? "Bạn đã là thành viên của nhóm này."
                    : invitePreview.requestStatus === "Pending"
                      ? "Yêu cầu của bạn đang chờ trưởng nhóm duyệt."
                      : "Gửi yêu cầu tham gia. Bạn chỉ được xem công việc và chat chung sau khi trưởng nhóm duyệt."}
                </p>
              </>
            ) : null}
            {(joinLink.error || error) && (
              <p className="team-error" role="alert">
                {joinLink.error || error}
              </p>
            )}
            <div className="team-actions">
              {invitePreview && (
                <button
                  className="btn primary"
                  disabled={
                    busy ||
                    previewLoading ||
                    ["Pending", "Removed"].includes(invitePreview.requestStatus)
                  }
                  onClick={() =>
                    run(async () => {
                      await requestJoin(joinLink.code);
                      closeJoinLink();
                    })
                  }
                >
                  {busy
                    ? "Đang tham gia…"
                    : invitePreview.alreadyMember
                      ? "Mở nhóm"
                      : invitePreview.requestStatus === "Pending"
                        ? "Đang chờ duyệt"
                        : invitePreview.requestStatus === "Removed"
                          ? "Không thể tham gia lại"
                          : "Gửi yêu cầu tham gia"}
                </button>
              )}
              <button
                className="btn secondary"
                disabled={busy}
                onClick={closeJoinLink}
              >
                {invitePreview ? "Để sau" : "Đóng lời mời"}
              </button>
            </div>
          </div>
        </TeamDialog>
      )}
      {removingMember && detail && (
        <RemoveTeamMemberDialog
          member={removingMember}
          self={removingMember.userId === uid}
          busy={busy}
          error={error}
          onClose={() => setRemovingMember(null)}
          onConfirm={() =>
            run(async () => {
              await post(
                `${detail.team.id}/members/${removingMember.userId}/remove`,
                { version: removingMember.version },
              );
              setRemovingMember(null);
              await changed();
            })
          }
        />
      )}
      {invite && detail && <TeamInviteDialog team={detail.team} onClose={() => setInvite(false)} />}
      {splittingTask && detail && (
        <TeamTaskSplitDialog task={splittingTask} members={detail.members} busy={busy} error={error}
          onClose={() => { setError(""); setSplittingTask(null); }}
          onSubmit={(children) => run(async () => {
            await post(`tasks/${splittingTask.id}/split`, { version: splittingTask.version, children });
            setSplittingTask(null);
            await changed();
            setTaskToOpen({ teamId: detail.team.id, taskId: splittingTask.id });
          })} />
      )}
      {claimOpen && (
        <TeamDialog
          title={claimRequest?.change ? "Xem lịch cho yêu cầu mới" : "Xem lịch trước khi nhận việc"}
          className="team-claim-dialog"
          subtitle={claimRequest?.task.name}
          busy={busy}
          wide
          onClose={cancelClaim}
        >
          <div className="team-claim-summary">
            <span>
              {icon("clock")}
              {minutes(claimRequest?.task.estimatedMinutes, "long")}
            </span>
            <span>
              {icon("calendar")}Hạn {dateFmt(claimRequest?.task.deadline)}{" "}
              {timeFmt(claimRequest?.task.deadline)}
            </span>
          </div>
          {!claimRequest?.change && <TeamChecklist items={claimRequest?.task.checklist} />}
          {error && (
            <div className="team-error" role="alert">
              {error}
            </div>
          )}
          {error && claim && (
            <button
              className="btn secondary"
              disabled={busy}
              onClick={() => openClaim(claimRequest.task, claimRequest.change)}
            >
              Tính lại theo lịch mới
            </button>
          )}
          {!claim && busy && (
            <div className="team-claim-loading" role="status">
              <span>{icon("spark")}</span>
              <h3>NORA đang tìm lịch phù hợp</h3>
              <p>
                NORA đang phân tích công việc, đối chiếu thời gian biểu và tìm
                lịch phù hợp riêng cho bạn…
              </p>
              <div className="team-analysis-steps">
                <span>Đọc công việc</span>
                <span>Đối chiếu lịch riêng</span>
                <span>Kiểm tra phương án</span>
              </div>
            </div>
          )}
          {!claim && !busy && (
            <div className="team-actions">
              <button
                className="btn primary"
                onClick={() =>
                  openClaim(claimRequest.task, claimRequest.change)
                }
              >
                Thử lại với lịch hiện tại
              </button>
              <button className="btn secondary" onClick={cancelClaim}>
                Đóng
              </button>
            </div>
          )}
          {claim && (
            <>
              {!claimRequest.change && claimRequest.task.availabilityCheck?.userId === uid &&
                claimRequest.task.availabilityCheck.canTake == null && (
                  <div className="team-availability-reply">
                    <p>Trưởng nhóm chỉ thấy câu trả lời. Bạn vẫn cần xác nhận riêng để nhận việc.</p>
                    <div className="team-actions">
                      {[true, false].map((canTake) => (
                        <button key={String(canTake)} type="button" className="btn secondary"
                          disabled={busy || (canTake && !claim.options.length)}
                          onClick={() => run(async () => {
                            const { task } = claimRequest;
                            await post(`tasks/${task.id}/availability/respond`, { version: task.version, canTake });
                            setClaim(null);
                            setClaimOpen(false);
                            await reload();
                          })}>
                          {canTake ? "Báo có thể nhận" : "Báo chưa thể nhận"}
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              <PrivateClaimPreview
                claim={claim}
                busy={busy}
                onConfirm={(option) =>
                  run(async () => {
                    await post(`claims/${claim.id}/confirm`, { option });
                    setUndo(claim.canUndo === false ? null : claim.id);
                    setClaim(null);
                    setClaimOpen(false);
                    await changed();
                    if (claimRequest?.task.checklist?.length)
                      setTaskToOpen({ teamId: detail.team.id, taskId: claim.taskId });
                  })
                }
                onCancel={cancelClaim}
                onDiscuss={() => {
                  setClaimOpen(false);
                  askNora(
                    `Chỉ tư vấn, chưa tạo công việc, chưa nhận task hay thay đổi lịch: tôi đang cân nhắc task nhóm "${claimRequest.task.name}", cần ${claimRequest.task.estimatedMinutes} phút, hạn ${dateFmt(claimRequest.task.deadline)} ${timeFmt(claimRequest.task.deadline)}. Hãy đọc lịch của tôi và giải thích những điều kiện cần bổ sung để nhận việc. Nếu cần thay đổi giờ có thể làm việc, hãy hỏi và chuẩn bị đề xuất để tôi xác nhận.`,
                  );
                }}
                onAdjust={
                  detail?.team.role === "Leader"
                    ? () => {
                        setClaimOpen(false);
                        openComposer(claimRequest.task);
                      }
                    : undefined
                }
              />
              {claim.failure?.needsAvailability && (
                <a className="btn secondary" href="#time" onClick={cancelClaim}>Mở Giờ làm việc</a>
              )}
              {claim.canUndo !== false && (claim.failure || claim.options?.length > 0) &&
                !claim.failure?.needsAvailability &&
                claim.failure?.code !== "NO_WORK_HOURS_BEFORE_DEADLINE" && (
                  <button
                    className="team-extended-option"
                    disabled={busy}
                    onClick={() =>
                      run(async () =>
                        setClaim(
                          await post(
                            `tasks/${claim.taskId}/preview?extend=true`,
                          ),
                        ),
                      )
                    }
                  >
                    {icon("refresh")} Cho phép thử dời phiên linh hoạt xa hơn,
                    tối đa 30 ngày và trong deadline cũ
                  </button>
                )}
            </>
          )}
        </TeamDialog>
      )}
      {returning && (
        <TeamDialog
          title="Trả việc về nhóm"
          subtitle="Xem tác động trước khi xác nhận."
          busy={busy}
          onClose={() => setReturning(null)}
        >
          <section
            className="team-return-content"
            aria-label="Xác nhận trả việc"
          >
            <p>Các phiên sau sẽ bị hủy. Lịch sử đã thực hiện được giữ lại.</p>
            <SessionList items={returning.sessions} showStatus={false} />
            {error && (
              <p role="alert" className="team-error">
                {error}
              </p>
            )}
            <div className="team-actions end">
              <button
                className="btn secondary"
                disabled={busy}
                onClick={() => setReturning(null)}
              >
                Hủy trả việc
              </button>
              <button
                className="btn primary"
                disabled={busy}
                onClick={() =>
                  run(async () => {
                    await post(`tasks/${returning.taskId}/return`, {
                      version: returning.version,
                      revision: returning.revision,
                    });
                    setReturning(null);
                    setUndo(null);
                    await changed();
                  })
                }
              >
                Xác nhận trả việc và hủy các phiên trên
              </button>
            </div>
          </section>
        </TeamDialog>
      )}
    </div>
  );
}
