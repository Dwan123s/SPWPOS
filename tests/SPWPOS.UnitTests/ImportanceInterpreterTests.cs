using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.UnitTests;

public sealed partial class VertexAiInterpreterTests
{
    [Theory] [InlineData("VertexAI")] [InlineData("Gemini")]
    public async Task Importance_uses_configured_flash_native_schema_and_only_task_context(string providerName)
    {
        var result = new ImportanceSuggestion("Important", "Đầu ra bắt buộc", .7, "Nghiệm thu proposal");
        using var provider = new Provider(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var http = new HttpClient(provider);
        Assert.Equal(result, await Interpreter(http, model: "gemini-3.8-flash", provider: providerName)
            .SuggestImportanceAsync(new("Nghiệm thu proposal", "Hợp nhất và kiểm tra đầu ra bắt buộc", ["Bản nháp"]), TestContext.Current.CancellationToken));
        Assert.EndsWith("/models/gemini-3.8-flash:generateContent", provider.Url!.AbsolutePath);
        using var body = JsonDocument.Parse(provider.Body);
        var generation = body.RootElement.GetProperty("generationConfig");
        // Gemini 3.8's shared thinking/answer budget is retained even for a short answer.
        Assert.Equal(16384, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("LOW", generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        var schema = providerName == "VertexAI" ? generation.GetProperty("responseJsonSchema") : generation.GetProperty("responseFormat").GetProperty("text").GetProperty("schema");
        Assert.Equal("number", schema.GetProperty("properties").GetProperty("confidence").GetProperty("type").GetString());
        using var prompt = JsonDocument.Parse(body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        Assert.Equal(3, prompt.RootElement.EnumerateObject().Count()); Assert.Equal("Nghiệm thu proposal", prompt.RootElement.GetProperty("name").GetString());
        var instructions = body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!;
        Assert.Contains("untrusted", instructions); Assert.Contains("Never use deadline proximity", instructions); Assert.Contains("human review", instructions);
    }
    [Fact] public async Task Importance_rejects_incomplete_provider_output()
    {
        using var provider = new Provider("{\"level\":\"Important\"}"); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http).SuggestImportanceAsync(new("Task", "", []), TestContext.Current.CancellationToken));
        Assert.Equal("IMPORTANCE_AI_INVALID", error.Code);
    }
}
