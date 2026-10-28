using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/agent"), Tags("Planning Agent")]
public sealed class AgentController(IPlanningAgentService service, ILogger<AgentController> logger, IAgentVoiceTranscriber voice, IAgentTimetableImageReader images) : WorkspaceController
{
    [HttpGet("capabilities")] public IActionResult Capabilities() { var caps = service.GetAgentCapabilities(); return Data(new { caps.AiAvailable, caps.MaxTasks, caps.Provider, VoiceAvailable = voice.VoiceAvailable, ImageAvailable = images.ImageAvailable,
        PolicyVersion = "2026-10-28.1", Features = new[] { "conversation-lifecycle", "usage-ledger", "planning-policy", "work-category", "verified-outcomes", "scoped-event-replanning", "bounded-clarification-recovery", "concise-confirmations", "vision-timetable", "chat-images" } }); }
    [HttpGet("usage")] public async Task<IActionResult> Usage([FromServices] IAiUsageService usage, CancellationToken ct) => Data(await usage.SummaryAsync(UserId, ct));
    [HttpGet("turns")] public async Task<IActionResult> History(CancellationToken ct) => Data(await service.GetAgentTurnsAsync(UserId, ct));
    [HttpGet("conversations")] public async Task<IActionResult> Conversations(CancellationToken ct, string? search = null, bool archived = false, string? cursor = null, int pageSize = 30) => Data(await service.GetConversationsAsync(UserId, search, archived, cursor, pageSize, ct));
    [HttpGet("conversations/{id:guid}/turns")] public async Task<IActionResult> ConversationTurns(Guid id, CancellationToken ct, string? cursor = null, int pageSize = 50) => Data(await service.GetConversationTurnsAsync(UserId, id, cursor, pageSize, ct));
    [HttpPut("conversations/{id:guid}")] public async Task<IActionResult> EditConversation(Guid id, ConversationEdit input, CancellationToken ct) => Data(await service.EditConversationAsync(UserId, id, input, ct));
    [HttpDelete("conversations/{id:guid}")] public async Task<IActionResult> DeleteConversation(Guid id, Guid version, CancellationToken ct) { await service.DeleteConversationAsync(UserId, id, version, ct); return Done(); }
    [HttpGet("conversations/{id:guid}/export")] public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var value = await service.ExportConversationAsync(UserId, id, ct);
        return Data(value);
    }
    [HttpGet("turns/{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        var image = await service.GetAgentImageAsync(UserId, id, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(Convert.FromBase64String(image.Data!), image.MimeType);
    }
    [HttpPost("turns"), RequestSizeLimit(8 * 1024 * 1024)] public async Task<IActionResult> Send(AgentTurnInput input, CancellationToken ct) => Data(await service.SendAgentTurnAsync(UserId, input, ct));
    [HttpPost("turns/stream"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task Stream(AgentTurnInput input, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        async Task Publish(AgentStreamEvent item, CancellationToken token)
        {
            await Response.WriteAsync($"event: {item.Type}\ndata: {JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web))}\n\n", token);
            await Response.Body.FlushAsync(token);
        }
        try
        {
            var turn = await service.SendAgentTurnStreamingAsync(UserId, input, Publish, ct);
            await Publish(new("result", Turn: turn), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (IOException) when (ct.IsCancellationRequested) { }
        catch (AuthException failure)
        {
            if (!ct.IsCancellationRequested) await Publish(new("error", Message: failure.Message, Code: failure.Code), ct);
        }
        catch (Exception failure)
        {
            logger.LogError("Agent stream failed with {FailureType}", failure.GetType().Name);
            if (!ct.IsCancellationRequested) await Publish(new("error", Message: "NORA chưa hoàn tất yêu cầu. Kiểm tra lịch sử rồi thử lại.", Code: "AGENT_STREAM_FAILED"), ct);
        }
    }
    [HttpPost("turns/{id:guid}/apply")] public async Task<IActionResult> Apply(Guid id, CancellationToken ct) => Data(await service.ApplyAgentTurnAsync(UserId, id, ct));
    [HttpPost("turns/{id:guid}/undo")] public async Task<IActionResult> Undo(Guid id, CancellationToken ct) => Data(await service.UndoAgentTurnAsync(UserId, id, ct));
}
