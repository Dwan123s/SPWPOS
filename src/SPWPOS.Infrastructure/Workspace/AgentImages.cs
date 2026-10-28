using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static AgentImage? StoredAgentImage(AgentTurn turn) => turn.ResultJson.Length == 0 ? null
        : JsonSerializer.Deserialize<AgentTurnView>(turn.ResultJson, AgentJson)?.Image;

    private static AgentImage? PublicAgentImage(AgentImage? image, Guid turnId) => image is null ? null
        : image with { Data = null, Url = $"/api/agent/turns/{turnId:D}/image" };

    private static void ValidateAgentImage(AgentImage? image)
    {
        if (image is null) return;
        Require(!string.IsNullOrWhiteSpace(image.Name) && image.Name.Length <= 200 && image.Url is null &&
            image.Data is { Length: > 0 } && image.Data.Length <= ((TimetableImageLimits.MaxBytes + 2) / 3) * 4,
            "IMAGE_SIZE", "Ảnh không hợp lệ hoặc vượt quá 5 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(image.Data!); }
        catch (FormatException) { throw new AuthException("IMAGE_FORMAT", "Dữ liệu ảnh chưa hợp lệ.", 400); }
        Require(bytes.Length <= TimetableImageLimits.MaxBytes && TimetableImageLimits.ValidImage(bytes, image.MimeType),
            "IMAGE_FORMAT", "Bạn chọn ảnh PNG, JPG hoặc WebP hợp lệ, tối đa 5 MB nhé.");
    }

    public async Task<AgentImage> GetAgentImageAsync(Guid uid, Guid turnId, CancellationToken ct)
    {
        var turn = await Mine<AgentTurn>(uid).AsNoTracking().SingleOrDefaultAsync(x => x.Id == turnId && x.Status != "DELETED", ct);
        Require(turn is not null && !await Mine<AgentConversation>(uid).AnyAsync(x => x.Id == turn!.ConversationId && x.DeletedAt != null, ct),
            "NOT_FOUND", "Không tìm thấy ảnh trong hội thoại của bạn.", 404);
        var image = StoredAgentImage(turn!);
        Require(image?.Data is { Length: > 0 }, "NOT_FOUND", "Tin nhắn này không có ảnh.", 404);
        return image!;
    }
}
