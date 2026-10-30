using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.TestSupport.NoraSandbox;

public sealed class ConversationActions
{
    private readonly Dictionary<string, (AgentTurnInput Input, AgentTurnView View)> users = [];
    public void Capture(AgentTurnInput input, AgentTurnView view) => users.Add("user_turn:" + (users.Count + 1), (input, view));
    public async Task<AgentTurnView> ExecuteAsync(SandboxEnvironment env, ConversationTurn turn, CancellationToken ct)
    {
        var args = turn.Params ?? new();
        (AgentTurnInput Input, AgentTurnView View) Target(string field) => users[args[field]!.GetValue<string>()];
        AgentTurnView HarnessResult(string status = "ANSWERED") => new(Guid.Empty, env.ConversationId, Guid.Empty,
            "", "Harness action: " + turn.Action, status, "", null, [], [], env.Clock.GetUtcNow());
        switch(turn.Action)
        {
            case "apply_turn":
                return await env.Agent(s => s.ApplyAgentTurnAsync(env.UserId, Target("target").View.Id, ct));
            case "undo_turn":
                return await env.Agent(s => s.UndoAgentTurnAsync(env.UserId, Target("target").View.Id, ct));
            case "advance_clock":
                env.Clock.Advance(TimeSpan.FromMinutes(args["minutes"]!.GetValue<int>()));
                return HarnessResult();
            case "resend_request":
                var original = Target("source").Input;
                var replay = original with {
                    RequestId = args["same_request_id"]?.GetValue<bool>() == true ? original.RequestId : Guid.NewGuid(),
                    Text = args["replace_text"]?.GetValue<string>() ?? original.Text
                };
                return await env.Agent(s => s.SendAgentTurnAsync(env.UserId, replay, ct));
            case "mutate_calendar_outside_conversation":
                if (args["add_event"] is { } item)
                {
                    var id = await env.Work(s => s.SaveEventAsync(env.UserId, null, new(item["name"]!.GetValue<string>(),
                        DateTimeOffset.Parse(item["start"]!.GetValue<string>(), CultureInfo.InvariantCulture),
                        DateTimeOffset.Parse(item["end"]!.GetValue<string>(), CultureInfo.InvariantCulture),
                        EventType: item["category"]!.GetValue<string>()), ct));
                    env.Ids[item["ref"]!.GetValue<string>()] = id;
                }
                else if (args["update_last_created_task"] is { } update)
                {
                    var id = users.Values.Last().View.Changes.Last(c => c.Kind == "TASK" && c.Before == "").TaskId!.Value;
                    var task = await env.Db(db => db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == id && t.UserId == env.UserId, ct), ct);
                    await env.Work(s => s.SaveTaskAsync(env.UserId, id, new(task.Name, task.Description, task.Deadline,
                        update["estimated_minutes"]!.GetValue<int>(), task.Priority, task.TaskType, task.Splittable,
                        task.MinimumSessionMinutes, task.CategoryId, task.ParentId, task.IsHardDeadline, task.CreationSource, task.Version), ct));
                }
                else throw new NotSupportedException("Unknown external mutation.");
                return HarnessResult("APPLIED");
            case "archive_current_conversation":
            case "delete_current_conversation":
                var conversation = await env.Db(async db => {
                    var row = await db.AgentConversations.SingleOrDefaultAsync(c => c.Id == env.ConversationId && c.UserId == env.UserId, ct);
                    if (row is not null) return row;
                    row = new() { Id = env.ConversationId, UserId = env.UserId, Title = "Harness conversation", CreatedAt = env.Clock.GetUtcNow(), UpdatedAt = env.Clock.GetUtcNow() };
                    db.AgentConversations.Add(row); await db.SaveChangesAsync(ct); return row;
                }, ct);
                if(turn.Action == "archive_current_conversation")
                    await env.Agent(s => s.EditConversationAsync(env.UserId, env.ConversationId, new(conversation.Title, true, conversation.Version), ct));
                else
                    await env.Agent(async s => { await s.DeleteConversationAsync(env.UserId, env.ConversationId, conversation.Version, ct); return true; });
                return HarnessResult();
            default: throw new NotSupportedException("Unsupported conversation action: " + turn.Action);
        }
    }
}
