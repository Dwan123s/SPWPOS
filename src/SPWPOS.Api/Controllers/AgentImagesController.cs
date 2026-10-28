using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Api.Controllers;

[Route("api/agent/images"), Tags("Planning Agent")]
public sealed class AgentImagesController(IAgentTimetableImageReader reader, IAiUsageService usage) : WorkspaceController
{
    [HttpPost("timetable"), RequestSizeLimit(TimetableImageLimits.MaxBytes), EnableRateLimiting("agent-image")]
    public async Task<IActionResult> ReadTimetable(CancellationToken ct)
    {
        // The chat sends this only when the person submits their selected image.
        if (Request.Headers["X-SPWPOS-Image-Consent"] != "1")
            throw new AuthException("IMAGE_CONSENT_REQUIRED", "Bạn bấm Gửi để cho phép NORA đọc ảnh bằng AI nhé.", 400);
        var mimeType = (Request.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (mimeType is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new AuthException("IMAGE_FORMAT", "Bạn chọn ảnh PNG, JPG hoặc WebP nhé.", 415);
        if (Request.ContentLength > TimetableImageLimits.MaxBytes)
            throw new AuthException("IMAGE_SIZE", "Ảnh vượt quá 5 MB. Bạn giảm kích thước hoặc cắt riêng phần thời khóa biểu nhé.", 413);
        using var image = new MemoryStream();
        var buffer = new byte[16384];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (image.Length + count > TimetableImageLimits.MaxBytes)
                throw new AuthException("IMAGE_SIZE", "Ảnh vượt quá 5 MB. Bạn cắt riêng phần thời khóa biểu nhé.", 413);
            image.Write(buffer, 0, count);
        }
        var bytes = image.ToArray();
        if (bytes.Length == 0 || !TimetableImageLimits.ValidImage(bytes, mimeType))
            throw new AuthException("IMAGE_FORMAT", "Ảnh trống hoặc không đúng định dạng. Bạn chọn lại ảnh PNG, JPG hoặc WebP nhé.", 400);
        // No image is persisted or exposed as an upload URL.
        return Data(await usage.RunAsync(UserId, "TIMETABLE_IMAGE", () => reader.ReadTimetableAsync(bytes, mimeType, ct), ct));
    }
}
