using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class AgentVisionApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
    private static AgentImage Image => new("Lịch học.png", "image/png", Convert.ToBase64String(Png));
    private static AgentImage TwoImages => Image with { AdditionalImages = [new("Lịch làm.png", "image/png", Convert.ToBase64String(Png.Concat(new byte[] { 9 }).ToArray()))] };
    private sealed class Factory : ApiFactory
    {
        public AgentAiTestDouble Ai { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        { base.ConfigureWebHost(builder); builder.ConfigureServices(s => s.AddSingleton<ITaskInterpreter>(Ai)); }
    }
    [Fact]
    public async Task Two_images_reach_AI_together_survive_followup_and_are_private_in_history()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Question = "Áp dụng từ ngày nào?" };
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Gộp hai thời khóa biểu trong ảnh và tìm giờ rảnh", Image: TwoImages);
        var first = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input)).GetProperty("data");
        Assert.Equal(2, f.Ai.LastImage!.GetImages().Length);
        Assert.Equal(TwoImages.AdditionalImages![0].Data, f.Ai.LastImage.AdditionalImages![0].Data);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("image").GetProperty("data").ValueKind);
        var secondImage = Assert.Single(first.GetProperty("image").GetProperty("additionalImages").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, secondImage.GetProperty("data").ValueKind);
        var url = secondImage.GetProperty("url").GetString()!;
        using var picture = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        Assert.Equal(Convert.FromBase64String(TwoImages.AdditionalImages[0].Data!), await picture.Content.ReadAsByteArrayAsync(Ct));
        using var invalidIndex = await client.GetAsync(url.Replace("index=1", "index=2"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, invalidIndex.StatusCode);
        using var anonymous = await f.ClientAsync(); using var denied = await anonymous.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var start = f.Clock.GetUtcNow().AddDays(1);
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Toán", start, start.AddHours(1), EventType: "CLASS"),
            new("Ca làm", start.AddHours(2), start.AddHours(3), EventType: "WORK")] };
        var followup = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            new AgentTurnInput(input.ConversationId, Guid.NewGuid(), "Áp dụng ngày mai trong 1 tuần"))).GetProperty("data");
        Assert.Equal("APPLIED", followup.GetProperty("status").GetString());
        Assert.Equal(2, f.Ai.LastImage.GetImages().Length);
        var events = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data").GetProperty("events");
        Assert.Equal(2, events.GetArrayLength());
        var history = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/turns")).GetProperty("data");
        Assert.All(history.EnumerateArray().Where(turn => turn.GetProperty("image").ValueKind == JsonValueKind.Object),
            turn => Assert.Equal(JsonValueKind.Null, Assert.Single(turn.GetProperty("image").GetProperty("additionalImages").EnumerateArray()).GetProperty("data").ValueKind));
    }

    [Fact]
    public async Task Invalid_second_image_or_more_than_two_are_rejected_and_second_image_is_part_of_request_identity()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Gộp hai thời khóa biểu", Image: TwoImages);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input with { Image = Image with { AdditionalImages = [new("bad.png", "image/png", "invalid")] } }, HttpStatusCode.BadRequest);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input with { Image = Image with { AdditionalImages = [.. TwoImages.AdditionalImages!, .. TwoImages.AdditionalImages!] } }, HttpStatusCode.BadRequest);
        Assert.Equal(0, f.Ai.Calls);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input with { Image = Image }, HttpStatusCode.Conflict);
        Assert.Equal(1, f.Ai.Calls);
    }
    [Fact]
    public async Task Vision_receives_the_original_image_and_chat_history_retains_a_private_image_after_saving_and_undo()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var start = f.Clock.GetUtcNow().AddDays(1);
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Toán", start, start.AddHours(1), EventType: "CLASS")] };
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Xếp lịch theo thời khóa biểu trong ảnh.", Image: Image);
        var result = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input)).GetProperty("data");
        Assert.Equal("APPLIED", result.GetProperty("status").GetString());
        Assert.Equal(input.Text, result.GetProperty("text").GetString()); Assert.Equal(Image, f.Ai.LastImage);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("image").GetProperty("data").ValueKind);
        var url = result.GetProperty("image").GetProperty("url").GetString()!;
        using var picture = await client.GetAsync(url, Ct); Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        Assert.Equal(Png, await picture.Content.ReadAsByteArrayAsync(Ct)); Assert.Equal("image/png", picture.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", picture.Headers.CacheControl!.ToString());
        using var anonymous = await f.ClientAsync(); using var denied = await anonymous.GetAsync(url, Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var history = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/turns")).GetProperty("data");
        Assert.Equal(url, Assert.Single(history.EnumerateArray()).GetProperty("image").GetProperty("url").GetString());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{result.GetProperty("id").GetGuid()}/undo");
        using var afterUndo = await client.GetAsync(url, Ct); Assert.Equal(HttpStatusCode.OK, afterUndo.StatusCode);
        var page = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/conversations")).GetProperty("data");
        var conversation = Assert.Single(page.GetProperty("items").EnumerateArray());
        await WorkspaceFlowTests.Send(client, HttpMethod.Delete, $"/api/agent/conversations/{input.ConversationId}?version={conversation.GetProperty("version").GetGuid()}");
        using var deleted = await client.GetAsync(url, Ct); Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task A_duration_or_date_followup_reuses_the_image_without_an_OCR_transcript()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Question = "Áp dụng từ ngày nào?" };
        var conversation = Guid.NewGuid();
        var first = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            new AgentTurnInput(conversation, Guid.NewGuid(), "Xếp thời khóa biểu trong ảnh", Image: Image))).GetProperty("data");
        Assert.Equal("NEEDS_INPUT", first.GetProperty("status").GetString());
        var start = f.Clock.GetUtcNow().AddDays(1);
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Toán", start, start.AddHours(1), EventType: "CLASS")] };
        var second = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            new AgentTurnInput(conversation, Guid.NewGuid(), "Áp dụng ngày mai trong 1 tuần"))).GetProperty("data");
        Assert.Equal("APPLIED", second.GetProperty("status").GetString()); Assert.Equal(Image, f.Ai.LastImage);
        Assert.Equal("Xếp thời khóa biểu trong ảnh", Assert.Single(f.Ai.History).Text);
    }

    [Fact]
    public async Task Changed_image_cannot_reuse_a_request_id_and_a_forged_image_never_reaches_AI()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Xem ảnh", Image: Image);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input);
        var error = await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            input with { Image = Image with { Data = Convert.ToBase64String(Png.Concat(new byte[] { 4 }).ToArray()) } }, HttpStatusCode.Conflict);
        Assert.Equal("AGENT_REQUEST_REUSED", error.GetProperty("error").GetProperty("code").GetString());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            input with { RequestId = Guid.NewGuid(), Image = Image with { Data = "invalid" } }, HttpStatusCode.BadRequest);
        Assert.Equal(1, f.Ai.Calls);
    }
}
