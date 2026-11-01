using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.UnitTests;

public sealed partial class VertexAiInterpreterTests
{
    private static readonly TeamBriefContext BriefContext = new(2, "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-11-01T00:00:00Z"), "brief.pdf", "Bổ sung deadline", null, "JVBERi0xLjc=");
    private static string BriefResponse => JsonSerializer.Serialize(new TeamBriefAnalysis("Xây ứng dụng", "", "Chia hai phần", [
        new("Thiết kế", "Thiết kế theo tài liệu", ["Yêu cầu 1"], ["ERD"], ["Đủ thực thể"], ["Phối hợp"], ["Trang 1"], 90, DateTimeOffset.Parse("2026-11-04T14:00:00Z"), "04/11/2026 21h")
    ], []), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Theory] [InlineData("VertexAI")] [InlineData("Gemini")]
    public async Task Team_brief_uses_configured_flash_model_and_native_pdf_with_structured_work_notes(string providerName)
    {
        using var provider = new Provider(BriefResponse); using var http = new HttpClient(provider);
        var result = await Interpreter(http, model: "gemini-3.8-flash", provider: providerName).AnalyzeTeamBriefAsync(BriefContext, TestContext.Current.CancellationToken);
        Assert.Equal("ERD", Assert.Single(result.Tasks).Deliverables.Single());
        Assert.EndsWith("/models/gemini-3.8-flash:generateContent", provider.Url!.AbsolutePath);
        using var body = JsonDocument.Parse(provider.Body);
        var parts = body.RootElement.GetProperty("contents")[0].GetProperty("parts");
        Assert.Equal("application/pdf", parts[1].GetProperty("inlineData").GetProperty("mimeType").GetString());
        Assert.Equal(BriefContext.PdfData, parts[1].GetProperty("inlineData").GetProperty("data").GetString());
        using var prompt = JsonDocument.Parse(parts[0].GetProperty("text").GetString()!);
        Assert.Equal(2, prompt.RootElement.GetProperty("memberCount").GetInt32());
        Assert.False(prompt.RootElement.TryGetProperty("pdfData", out _));
        var instructions = body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!;
        Assert.Contains("UNTRUSTED", instructions); Assert.Contains("Never invent deadlines", instructions);
        Assert.Equal("LOW", body.RootElement.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }
    [Fact] public async Task Team_brief_text_input_is_sent_without_pdf_and_malformed_output_is_rejected()
    {
        using var provider = new Provider(BriefResponse); using var http = new HttpClient(provider);
        await Interpreter(http).AnalyzeTeamBriefAsync(BriefContext with { PdfData = null, DocumentText = "Yêu cầu hệ thống" }, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(provider.Body);
        Assert.Single(body.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray());
        using var badProvider = new Provider("{\"summary\":\"incomplete\"}"); using var badHttp = new HttpClient(badProvider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(badHttp).AnalyzeTeamBriefAsync(BriefContext, TestContext.Current.CancellationToken));
        Assert.Equal("TEAM_AI_INVALID_RESPONSE", error.Code);
    }
}
