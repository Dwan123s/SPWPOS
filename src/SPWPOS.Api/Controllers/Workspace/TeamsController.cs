using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/teams"), Tags("Private team planning")]
public sealed class TeamsController(IPrivateTeamService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Data(await service.ListTeamsAsync(UserId, ct));
    [HttpPost] public async Task<IActionResult> Create(TeamNameInput input, CancellationToken ct) => Data(new { id = await service.CreateTeamAsync(UserId, input, ct) });
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Data(await service.GetTeamAsync(UserId, id, ct));
    [HttpGet("{id:guid}/messages")] public async Task<IActionResult> Messages(Guid id, [FromQuery] long? after, [FromQuery] long? before, CancellationToken ct) => Data(await service.GetTeamChatAsync(UserId, id, after, before, ct));
    [HttpPost("{id:guid}/messages")] public async Task<IActionResult> SendMessage(Guid id, TeamChatInput input, CancellationToken ct) => Data(await service.SendTeamChatAsync(UserId, id, input, ct));
    [HttpPost("{id:guid}/invites")] public async Task<IActionResult> Invite(Guid id, CancellationToken ct) => Data(await service.CreateTeamInviteAsync(UserId, id, ct));
    [HttpGet("invites/{code}")] public async Task<IActionResult> PreviewInvite(string code, CancellationToken ct) => Data(await service.PreviewTeamInviteAsync(UserId, code, ct));
    [HttpDelete("{id:guid}/invites/{inviteId:guid}")] public async Task<IActionResult> Revoke(Guid id, Guid inviteId, CancellationToken ct) { await service.RevokeTeamInviteAsync(UserId, id, inviteId, ct); return Done(); }
    [HttpPost("join")] public async Task<IActionResult> Join(TeamTextInput input, CancellationToken ct)
    {
        var id = await service.JoinTeamAsync(UserId, input.Text, ct);
        return Data(new { id, status = (await service.ListTeamsAsync(UserId, ct)).Any(x => x.Id == id) ? "Approved" : "Pending" });
    }
    [HttpGet("join-requests")] public async Task<IActionResult> MyRequests(CancellationToken ct) => Data(await service.ListMyTeamJoinRequestsAsync(UserId, ct));
    [HttpPost("{id:guid}/join-requests/{memberId:guid}/review")] public async Task<IActionResult> ReviewJoin(Guid id, Guid memberId, TeamJoinReviewInput input, CancellationToken ct)
    { await service.ReviewTeamJoinRequestAsync(UserId, id, memberId, input, ct); return Done(); }
    [HttpPost("{id:guid}/members/{memberId:guid}/remove")] public async Task<IActionResult> Remove(Guid id, Guid memberId, TeamVersionInput input, CancellationToken ct) { await service.RemoveTeamMemberAsync(UserId, id, memberId, input.Version, ct); return Done(); }
    [HttpPost("{id:guid}/drafts")] public async Task<IActionResult> Draft(Guid id, TeamTextInput input, CancellationToken ct) => Data(await service.DraftTeamTaskAsync(UserId, id, input, ct));
    [HttpPost("{id:guid}/drafts/{proposalId:guid}/confirm")] public async Task<IActionResult> ConfirmDraft(Guid id, Guid proposalId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] TeamTaskRecipientInput? input, CancellationToken ct)
        => Data(new { id = await service.ConfirmTeamTaskDraftAsync(UserId, id, proposalId, ct, input) });
    [HttpPost("{id:guid}/briefs"), RequestSizeLimit(12000000)] public async Task<IActionResult> Brief(Guid id, TeamBriefInput input, CancellationToken ct) => Data(await service.DraftTeamBriefAsync(UserId, id, input, ct));
    [HttpPost("{id:guid}/briefs/{proposalId:guid}/edit")] public async Task<IActionResult> EditBrief(Guid id, Guid proposalId, TeamDraftEditInput input, CancellationToken ct)
        => Data(await service.EditTeamBriefDraftAsync(UserId, id, proposalId, input, ct));
    [HttpPost("{id:guid}/briefs/{proposalId:guid}/confirm")] public async Task<IActionResult> ConfirmBrief(Guid id, Guid proposalId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] TeamBriefConfirmInput? input, CancellationToken ct)
        => Data(await service.ConfirmTeamBriefAsync(UserId, id, proposalId, ct, input));
    [HttpPost("tasks/{id:guid}/recipient")] public async Task<IActionResult> Recipient(Guid id, TeamTaskRecipientChangeInput input, CancellationToken ct)
    { await service.ChangeTeamTaskRecipientAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("tasks/{id:guid}/preview")] public async Task<IActionResult> Preview(Guid id, CancellationToken ct, [FromQuery] bool extend = false) => Data(await service.PreviewTeamClaimAsync(UserId, id, ct, extend));
    [HttpPost("claims/{id:guid}/confirm")] public async Task<IActionResult> Claim(Guid id, ClaimConfirmInput input, CancellationToken ct) { await service.ConfirmTeamClaimAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("claims/{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await service.CancelTeamClaimAsync(UserId, id, ct); return Done(); }
    [HttpPost("claims/{id:guid}/undo")] public async Task<IActionResult> Undo(Guid id, CancellationToken ct) { await service.UndoTeamClaimAsync(UserId, id, ct); return Done(); }
    [HttpPost("tasks/{id:guid}/return/preview")] public async Task<IActionResult> PreviewReturn(Guid id, CancellationToken ct) => Data(await service.PreviewTeamReturnAsync(UserId, id, ct));
    [HttpPost("tasks/{id:guid}/return")] public async Task<IActionResult> Return(Guid id, TeamReturnInput input, CancellationToken ct) { await service.ReturnTeamTaskAsync(UserId, id, input.Version, ct, input.Revision); return Done(); }
    [HttpPost("tasks/{id:guid}/progress")] public async Task<IActionResult> Progress(Guid id, TeamProgressInput input, CancellationToken ct) { await service.UpdateTeamProgressAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("tasks/{id:guid}/completion/review")] public async Task<IActionResult> ReviewCompletion(Guid id, TeamCompletionReviewInput input, CancellationToken ct) { await service.ReviewTeamCompletionAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("tasks/{id:guid}/completion/submit"), RequestSizeLimit(29000000)] public async Task<IActionResult> SubmitCompletion(Guid id, TeamCompletionSubmitInput input, CancellationToken ct) { await service.SubmitTeamCompletionAsync(UserId, id, input, ct); return Done(); }
    [HttpGet("tasks/{id:guid}/completion/files/{fileId:guid}")] public async Task<IActionResult> CompletionFile(Guid id, Guid fileId, CancellationToken ct)
    {
        var file = await service.DownloadTeamCompletionFileAsync(UserId, id, fileId, ct);
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        return File(file.Content, file.MimeType, file.Name);
    }
    [HttpGet("tasks/{id:guid}/source/files/{fileId:guid}")] public async Task<IActionResult> SourceFile(Guid id, Guid fileId, CancellationToken ct)
    {
        var file = await service.DownloadTeamSourceFileAsync(UserId, id, fileId, ct);
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.MimeType, file.Name);
    }
    [HttpPost("tasks/{id:guid}/source"), RequestSizeLimit(12000000)] public async Task<IActionResult> AttachSource(Guid id, TeamSourceFileInput input, CancellationToken ct)
    {
        await service.AttachTeamSourceFileAsync(UserId, id, input, ct); return Done();
    }
    [HttpPost("tasks/{id:guid}/changes")] public async Task<IActionResult> Change(Guid id, TeamTextInput input, CancellationToken ct) => Data(await service.ProposeTeamTaskChangeAsync(UserId, id, input, ct));
    [HttpPost("tasks/{id:guid}/changes/preview")] public async Task<IActionResult> PreviewChange(Guid id, CancellationToken ct) => Data(await service.PreviewTeamTaskChangeAsync(UserId, id, ct));
    [HttpPost("tasks/{id:guid}/changes/reject")] public async Task<IActionResult> Reject(Guid id, TeamVersionInput input, CancellationToken ct) { await service.RejectTeamTaskChangeAsync(UserId, id, input.Version, ct); return Done(); }
    [HttpGet("/api/time/policy")] public async Task<IActionResult> Policy(CancellationToken ct) => Data(await service.GetAvailabilityPolicyAsync(UserId, ct));
    [HttpGet("/api/time/policy/slots")] public async Task<IActionResult> PolicySlots(CancellationToken ct) => Data(await service.GetPolicySlotsAsync(UserId, ct));
    [HttpPost("/api/time/policy/preview")] public async Task<IActionResult> PreviewPolicy(CancellationToken ct, [FromQuery] Guid? timetableTurnId = null) => Data(await service.PreviewAvailabilityPolicyAsync(UserId, ct, timetableTurnId));
    [HttpPost("/api/time/policy/draft")] public async Task<IActionResult> DraftPolicy(TeamTextInput input, CancellationToken ct, [FromQuery] Guid? timetableTurnId = null) => Data(await service.DraftAvailabilityPolicyAsync(UserId, input, ct, timetableTurnId));
    [HttpPost("/api/time/policy/{id:guid}/confirm")] public async Task<IActionResult> ConfirmPolicy(Guid id, CancellationToken ct) { await service.ConfirmAvailabilityPolicyAsync(UserId, id, ct); return Done(); }
}
