import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, dateFmt, timeFmt, icon } from "./ui.jsx";
import { askNora } from "./agent-handoff.js";
import { TeamChat } from "./team-chat.jsx";
import {
  MyJoinRequests,
  TeamJoinRequests,
  RemoveTeamMemberDialog,
} from "./team-membership.jsx";
import "./team-membership.css";
import {
  CompletionFileUpload,
  CompletionReport,
} from "./team-completion-files.jsx";
import {
  captureTeamInvite,
  clearTeamInvite,
  parseTeamInvite,
  teamInviteLink,
} from "./team-invite-link.js";
import { AvailabilityPolicyCard } from "./availability-policy.jsx";
import { SessionList } from "./team-session-list.jsx";
import { TeamScheduleCompare } from "./team-schedule-compare.jsx";
export { SessionList } from "./team-session-list.jsx";
import {
  GroupHub,
  TeamHeader,
  TeamMembers,
  TaskBoard,
  TeamTaskComposer,
  TeamDialog,
  TeamActivity,
} from "./team-components.jsx";
import "./team-workspace.css";
import "./team-task-board.css";
import "./team-task-detail.css";
import "./team-recipient-select.css";
import { takeEisenhowerTeamTask } from "./eisenhower.jsx";
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
            <h3>Phân tích từ NORA</h3>
            <p>{claim.analysis}</p>
          </div>
        </div>
      )}
      {claim.warnings.map((w, i) => (
        <p role="status" className="team-error" key={i}>
          {w}
        </p>
      ))}
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
                <strong>{claim.failure.requiredMinutes} phút</strong>
              </div>
              <div>
                <span>Quỹ có thể xếp trước hạn</span>
                <strong>{claim.failure.capacityMinutes} phút</strong>
              </div>
              {claim.failure.missingMinutes > 0 && (
                <div>
                  <span>Còn thiếu ít nhất</span>
                  <strong>{claim.failure.missingMinutes} phút</strong>
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
            {icon("check")} Xác nhận phương án {o.index + 1}
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
  const [eisenhowerTaskId, setEisenhowerTaskId] = useState(null);
  const [teams, setTeams] = useState([]),
    [detail, setDetail] = useState(null),
    [loadingTeams, setLoadingTeams] = useState(true);
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [setupOpen, setSetupOpen] = useState(false);
  const [taskDetailOpen, setTaskDetailOpen] = useState(false);
  const [invite, setInvite] = useState(null),
    [copied, setCopied] = useState(false);
  const [composerOpen, setComposerOpen] = useState(false),
    [text, setText] = useState(""),
    [changeTask, setChangeTask] = useState(null),
    [draft, setDraft] = useState(null);
  const [briefFile, setBriefFile] = useState(null);
  const [splitBrief, setSplitBrief] = useState(false);
  const [claim, setClaim] = useState(null),
    [claimOpen, setClaimOpen] = useState(false),
    [claimRequest, setClaimRequest] = useState(null),
    [undo, setUndo] = useState(null),
    [returning, setReturning] = useState(null);
  const uid = state.workspace?.profile?.id;
  const [completion, setCompletion] = useState(null);
  const [reviewNote, setReviewNote] = useState("");
  const [completionFiles, setCompletionFiles] = useState([]);
  const [completionNote, setCompletionNote] = useState("");
  const [readingCompletionFiles, setReadingCompletionFiles] = useState(false);
  const [joinLink, setJoinLink] = useState(() => captureTeamInvite());
  const [invitePreview, setInvitePreview] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [joinRequests, setJoinRequests] = useState([]);
  const [joinMessage, setJoinMessage] = useState("");
  const [removingMember, setRemovingMember] = useState(null);
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
    if (reviewTeamId !== detail?.team.id || !joinReviewRef.current) return;
    joinReviewRef.current.focus({ preventScroll: true });
    joinReviewRef.current.scrollIntoView?.({
      behavior: "smooth",
      block: "center",
    });
    setReviewTeamId(null);
  }, [reviewTeamId, detail?.team.id]);
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
      const requested = takeEisenhowerTeamTask();
      if (!requested) return;
      try {
        const next = await api(`/api/teams/${requested.teamId}`);
        if (live) {
          setDetail(next);
          setEisenhowerTaskId(requested.taskId);
        }
      } catch (failure) {
        if (live) setError(failure.message);
      }
    };
    open();
    window.addEventListener("eisenhower-open-team", open);
    return () => {
      live = false;
      window.removeEventListener("eisenhower-open-team", open);
    };
  }, []);
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
    !!invite ||
    !!returning ||
    !!completion ||
    !!removingMember ||
    !!joinLink;
  const ready =
    !!state.workspace?.events?.length &&
    (!!state.workspace?.availabilityPolicy?.confirmed ||
      !!state.workspace?.availability?.length ||
      !!state.workspace?.exceptions?.some((x) => x.isAvailable));
  return (
    <div className="team-hub">
      <GroupHub
        teams={teams}
        loading={loadingTeams}
        onDialogChange={setSetupOpen}
        selected={detail?.team.id}
        busy={busy}
        error={error}
        onSelect={(id) =>
          run(async () => {
            setDetail(await api(`/api/teams/${id}`));
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
            setCopied(false);
            setInvite(await post(`${id}/invites`));
          })
        }
      />
      {joinMessage && (
        <p className="team-success" role="status">
          {joinMessage}
        </p>
      )}
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
          <TeamHeader
            detail={detail}
            busy={busy}
            onInvite={() =>
              run(async () => {
                setCopied(false);
                setInvite(await post(`${detail.team.id}/invites`));
              })
            }
            onRefresh={() => run(() => reload())}
            onCompose={() => openComposer()}
          />
          <div className="team-workspace-columns">
            <div className="team-work-main">
              <TaskBoard
                key={detail.team.id}
                detail={detail}
                openTaskId={eisenhowerTaskId}
                onImportanceChanged={() => run(() => reload())}
                error={error}
                onDialogChange={setTaskDetailOpen}
                onRecipientChange={(task, userId) =>
                  run(async () => {
                    await post(`tasks/${task.id}/recipient`, {
                      version: task.version,
                      userId,
                    });
                    await reload();
                  })
                }
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
                onProgress={(task, progress) => {
                  if (progress === 100) {
                    setError("");
                    setCompletionFiles([]);
                    setCompletionNote("");
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
              <TeamActivity detail={detail} />
            </div>
            <aside className="team-work-sidebar">
              <TeamChat
                key={detail.team.id}
                teamId={detail.team.id}
                name={detail.team.name}
              />
              <TeamMembers
                detail={detail}
                uid={uid}
                busy={busy}
                onRemove={(member) => {
                  setError("");
                  setRemovingMember(member);
                }}
              />
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
              <section
                className={`team-personal-readiness${ready ? " ready" : ""}`}
              >
                <span>{icon(ready ? "shield" : "calendar")}</span>
                <div>
                  <h3>
                    {ready
                      ? "Lịch cá nhân đã sẵn sàng"
                      : "Chuẩn bị lịch để nhận task"}
                  </h3>
                  <p>
                    {ready
                      ? "NORA dùng lịch của bạn để tìm chỗ phù hợp. Nhóm không xem được thông tin này."
                      : "Gửi thời khóa biểu và xác nhận giờ sinh hoạt. NORA sẽ giúp bạn sắp xếp công việc quanh lịch bận."}
                  </p>
                  <button
                    className="team-text-button"
                    onClick={() =>
                      askNora(
                        "Tôi muốn gửi thời gian biểu và xác nhận giờ có thể làm việc để nhận task nhóm.",
                      )
                    }
                  >
                    {ready ? "Trao đổi với NORA" : "Thiết lập với NORA"}{" "}
                    {icon("arrow")}
                  </button>
                </div>
              </section>
            </aside>
          </div>
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
      {completion && (
        <TeamDialog
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
              <CompletionReport task={completion.task} />
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
                disabled={busy || readingCompletionFiles}
                onClick={() =>
                  run(async () => {
                    if (completion.mode === "submit")
                      await post(
                        `tasks/${completion.task.id}/completion/submit`,
                        {
                          version: completion.task.version,
                          requestId: completion.requestId,
                          note: completionNote,
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
      {invite && (
        <TeamDialog
          title="Mời thành viên vào nhóm"
          subtitle={detail?.team.name}
          busy={busy}
          onClose={() => setInvite(null)}
        >
          <div className="team-invite-content">
            <span className="team-tile-icon">{icon("users")}</span>
            <p>Gửi link mời để thành viên mở và tham gia nhóm.</p>
            <label className="team-invite-link-field">
              Link tham gia nhóm
              <input
                readOnly
                value={teamInviteLink(invite.code)}
                onFocus={(e) => e.target.select()}
              />
            </label>
            <button
              className="btn primary"
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(
                    teamInviteLink(invite.code),
                  );
                  setCopied("link");
                } catch {
                  setError("Chọn và sao chép link trong ô ở trên để chia sẻ.");
                }
              }}
            >
              {icon("copy")}{" "}
              {copied === "link" ? "Đã sao chép link" : "Sao chép link mời"}
            </button>
            <details className="team-invite-code">
              <summary>Hoặc dùng mã mời</summary>
              <code>{invite.code}</code>
            </details>
            <p className="team-muted">
              Hết hạn {dateFmt(invite.expiresAt)} · Mã có thể được thu hồi bất
              cứ lúc nào. Thu hồi sẽ vô hiệu hóa cả mã và link mời này.
            </p>
            {error && (
              <p role="alert" className="team-error">
                {error}
              </p>
            )}
            <div className="team-actions">
              <button
                className="btn primary"
                onClick={async () => {
                  try {
                    await navigator.clipboard.writeText(invite.code);
                    setCopied("code");
                  } catch {
                    setError("Chọn và sao chép mã ở trên để chia sẻ.");
                  }
                }}
              >
                {copied === "code" ? "Đã sao chép mã" : "Sao chép mã mời"}
              </button>
              <button
                className="btn secondary"
                disabled={busy}
                onClick={() =>
                  run(async () => {
                    await api(
                      `/api/teams/${detail.team.id}/invites/${invite.id}`,
                      "DELETE",
                    );
                    setInvite(null);
                  })
                }
              >
                Thu hồi lời mời
              </button>
            </div>
          </div>
        </TeamDialog>
      )}
      {claimOpen && (
        <TeamDialog
          title="NORA · Nhận task và xếp lịch riêng"
          className="team-claim-dialog"
          subtitle={claimRequest?.task.name}
          busy={busy}
          wide
          onClose={cancelClaim}
        >
          <div className="team-claim-summary">
            <span>
              {icon("clock")}
              {claimRequest?.task.estimatedMinutes} phút
            </span>
            <span>
              {icon("calendar")}Hạn {dateFmt(claimRequest?.task.deadline)}{" "}
              {timeFmt(claimRequest?.task.deadline)}
            </span>
          </div>
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
                <AvailabilityPolicyCard
                  embedded
                  autoPreview
                  onWorkspaceChanged={async () => {
                    await changed();
                    await openClaim(claimRequest.task, claimRequest.change);
                  }}
                />
              )}
              {claim.canUndo !== false &&
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
