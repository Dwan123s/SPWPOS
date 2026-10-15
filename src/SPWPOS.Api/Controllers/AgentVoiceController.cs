using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Api.Controllers;

[Route("api/agent/voice"), Tags("Planning Agent")]
public sealed class AgentVoiceController(IAgentVoiceTranscriber voice, IAiUsageService usage) : WorkspaceController
{
    [HttpPost("transcribe"), RequestSizeLimit(VoiceLimits.MaxAudioBytes), EnableRateLimiting("agent-voice")]
    public async Task<IActionResult> Transcribe(CancellationToken ct)
    {
        var mimeType = (Request.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (mimeType is not ("audio/webm" or "audio/ogg" or "audio/mp4" or "audio/wav"))
            throw new AuthException("VOICE_AUDIO_FORMAT", "Định dạng bản ghi chưa được hỗ trợ.", 415);
        if (Request.ContentLength > VoiceLimits.MaxAudioBytes)
            throw new AuthException("VOICE_AUDIO_SIZE", "Bản ghi quá lớn. Hãy ghi một đoạn ngắn hơn.", 413);
        // Keep the short clip in memory only, never in form-upload buffers or on disk.
        using var audio = new MemoryStream();
        var buffer = new byte[16384];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (audio.Length + count > VoiceLimits.MaxAudioBytes)
                throw new AuthException("VOICE_AUDIO_SIZE", "Bản ghi quá lớn. Hãy ghi một đoạn ngắn hơn.", 413);
            audio.Write(buffer, 0, count);
        }
        var bytes = audio.ToArray();
        if (bytes.Length == 0 || !VoiceLimits.ValidAudio(bytes, mimeType))
            throw new AuthException("VOICE_AUDIO_FORMAT", "Bản ghi trống hoặc chưa đúng định dạng. Hãy thử lại.", 400);
        return Data(await usage.RunAsync(UserId,"VOICE",()=>voice.TranscribeAsync(bytes, mimeType, ct),ct));
    }
}
