using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;

namespace SPWPOS.UnitTests;

public sealed partial class NoraAgentRuntimeTests
{
    private static AgentImage ContextImages => new("Lịch học.png", "image/png", "iVBORw0KGgo=",
        AdditionalImages: [new("Lịch làm.jpg", "image/jpeg", "/9j/AA==")]);

    [Theory]
    [InlineData(false, "16/11/2026")]
    [InlineData(true, "Lập lịch tuần sau theo ảnh đã gửi")]
    public async Task Original_images_survive_answered_turns_and_short_followups(bool answeredUpload, string followup)
    {
        var image = ContextImages;
        Guid uploadId = Guid.Empty;
        var model = new Model { Decision = (request, number) => {
            var parts = request.Contents[0].GetProperty("parts");
            var data = JsonDocument.Parse(parts[0].GetProperty("text").GetString()!).RootElement;
            if (number == 1 || data.GetProperty("latestMessage").GetString() == followup)
            {
                Assert.Equal(3, parts.GetArrayLength());
                Assert.Equal(image.Data, parts[1].GetProperty("inlineData").GetProperty("data").GetString());
                Assert.Equal(image.AdditionalImages![0].Data, parts[2].GetProperty("inlineData").GetProperty("data").GetString());
                var context = data.GetProperty("imageContext");
                Assert.Equal(number != 1, context.GetProperty("reused").GetBoolean());
                if (number != 1) Assert.Equal(uploadId, context.GetProperty("sourceTurnId").GetGuid());
            }
            return Task.FromResult(number == 1 && !answeredUpload
                ? Call("ask_user", new { reply = "Đã xem ảnh.", question = "Áp dụng từ ngày nào?", options = Array.Empty<string>() }, number)
                : Understood(Call("reply_to_user", new { reply = "Mình đã hiểu thông tin bạn gửi." }, number), "Advice"));
        } };
        await using var h = await Harness(model, 2);
        var user = await h.RegisterAsync();
        var conversation = Guid.NewGuid();
        var first = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Đọc hai ảnh thời khóa biểu này, chưa lưu lịch.", Image: image), Ct));
        uploadId = first.Id;
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xin chào Nora"), Ct));
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Giải thích cách sắp xếp công việc"), Ct));
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Cảm ơn bạn"), Ct));
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), followup), Ct));
        Assert.Equal("ANSWERED", result.Status);
        Assert.Null(result.Image);
        Assert.Equal(image.Data, (await Agent(h, s => s.GetAgentImageAsync(user.Id, first.Id, Ct))).Data);
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.WorkTasks.AnyAsync(Ct)));
    }

    [Fact]
    public async Task The_latest_image_bundle_replaces_older_images_and_stays_in_its_conversation()
    {
        var image = ContextImages;
        var newer = new AgentImage("Lịch mới.png", "image/png", "iVBORw0KGgoAAA==");
        AgentImage? expected = image;
        var model = new Model { Decision = (request, number) => {
            var parts = request.Contents[0].GetProperty("parts");
            Assert.Equal(expected is null ? 1 : expected.GetImages().Length + 1, parts.GetArrayLength());
            if (expected is not null)
                Assert.Equal(expected.Data, parts[1].GetProperty("inlineData").GetProperty("data").GetString());
            return Task.FromResult(Understood(Call("reply_to_user", new { reply = "Đã đọc yêu cầu." }, number), "Advice"));
        } };
        await using var h = await Harness(model, 2);
        var user = await h.RegisterAsync();
        var conversation = Guid.NewGuid();
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Đọc ảnh cũ, chưa lưu", Image: image), Ct));
        expected = newer;
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Đọc ảnh mới thay cho ảnh trước", Image: newer), Ct));
        var reused = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Cho biết các buổi học trong ảnh mới"), Ct));
        Assert.Equal("ANSWERED", reused.Status);
        expected = null;
        var unrelated = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Cho tôi lời khuyên học tập"), Ct));
        Assert.Equal("ANSWERED", unrelated.Status);
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public async Task Image_reuse_obeys_the_conversation_history_boundary(int interveningTurns, bool retained)
    {
        var model = new Model { Decision = (request, number) => {
            var parts = request.Contents[0].GetProperty("parts");
            Assert.Equal(number == 1 || retained ? 3 : 1, parts.GetArrayLength());
            return Task.FromResult(Understood(Call("reply_to_user", new { reply = "Đã đọc yêu cầu." }, number), "Advice"));
        } };
        await using var h = await Harness(model, 2);
        var user = await h.RegisterAsync();
        var conversation = Guid.NewGuid();
        await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Đọc ảnh, chưa lưu lịch", Image: ContextImages), Ct));
        for (var index = 0; index < interveningTurns; index++)
            await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xin chào Nora"), Ct));
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Giải thích lại ảnh đã gửi", AutoApply: false), Ct));
        Assert.Equal("ANSWERED", result.Status);
    }
}
