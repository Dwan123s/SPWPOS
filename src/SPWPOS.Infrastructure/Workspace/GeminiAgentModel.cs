using System.Net.Http.Json;
using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class TaskInterpreter
{
    public bool AgentAvailable => Capabilities.AiAvailable && IsGoogle;
    public bool SupportsRequestUnderstanding => true;
    internal static string AgentRuntimeInstructions => NoraAssistantInstructions;

    public async Task<NoraModelReply> NextAgentStepAsync(NoraModelRequest input, CancellationToken ct)
    {
        if (!AgentAvailable) throw new AuthException("AGENT_AI_NOT_CONFIGURED", "Agent cần Gemini hoặc Vertex AI được cấu hình.", 503);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, AiEndpoint(config["AiSettings:Model"]!));
            await AuthenticateGoogleAsync(request, "AGENT", timeout.Token);
            var generation = new Dictionary<string, object> { ["maxOutputTokens"] = UsesGemini38 ? 16384 : 6000 };
            if (!UsesGemini38) generation["candidateCount"] = 1;
            if (GoogleThinkingLevel is { } thinking) generation["thinkingConfig"] = new { thinkingLevel = thinking };
            var offeredTools = NoraRequestPolicy.OfferedTools(input);
            request.Content = JsonContent.Create(new {
                systemInstruction = new { parts = new[] { new { text = input.Instructions } } },
                contents = input.Contents,
                tools = new[] { new { functionDeclarations = offeredTools.Select(tool => new {
                    name = tool.Name, description = tool.Description, parametersJsonSchema = NoraRequestPolicy.Parameters(tool, input.RequireUnderstanding)
                }).ToArray() } },
                toolConfig = new { functionCallingConfig = new { mode = "ANY", allowedFunctionNames = offeredTools.Select(x => x.Name).ToArray() } },
                generationConfig = generation
            });
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) throw await GoogleFailureDetailsAsync(response, new AuthException(
                response.StatusCode switch { System.Net.HttpStatusCode.TooManyRequests => "AGENT_AI_QUOTA_EXCEEDED",
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "AGENT_AI_AUTH_FAILED",
                    System.Net.HttpStatusCode.NotFound => "AGENT_AI_MODEL_UNAVAILABLE", _ => "AGENT_AI_REQUEST_FAILED" },
                "Dịch vụ AI chưa thực hiện được bước tiếp theo. Lịch chưa thay đổi.", 503), timeout.Token);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            usageCollector?.Read(payload.RootElement);
            var candidate = payload.RootElement.GetProperty("candidates")[0];
            if (candidate.GetProperty("finishReason").GetString() != "STOP") throw new JsonException("Incomplete agent step");
            // Preserve the full content, including thoughtSignature metadata, for
            // the next turn. Never forward this opaque content to the browser.
            var content = candidate.GetProperty("content").Clone();
            if (content.GetProperty("role").GetString() != "model") throw new JsonException("Invalid model role");
            var calls = new List<NoraToolCall>();
            NoraRequestUnderstanding? understanding = null;
            foreach (var part in content.GetProperty("parts").EnumerateArray())
            {
                if (!part.TryGetProperty("functionCall", out var function)) continue;
                var name = function.GetProperty("name").GetString()!;
                if (input.RequireUnderstanding && !offeredTools.Any(t => t.Name == name)) throw new JsonException("Tool not offered in this step");
                var args = function.GetProperty("args").Clone();
                if (args.ValueKind != JsonValueKind.Object || args.GetRawText().Length > 40000 || name.Length > 80) throw new JsonException("Invalid function call");
                if (input.RequireUnderstanding)
                {
                    var value = args.GetProperty("requestUnderstanding").Deserialize<NoraRequestUnderstanding>(Json)
                        ?? throw new JsonException("Missing request understanding");
                    NoraRequestPolicy.Validate(value);
                    if (understanding is not null && JsonSerializer.Serialize(understanding) != JsonSerializer.Serialize(value))
                        throw new JsonException("Conflicting request understanding");
                    understanding = value;
                    // Keep original signed provider content untouched in Content;
                    // only remove routing metadata from the server tool arguments.
                    args = JsonSerializer.SerializeToElement(args.EnumerateObject().Where(p => p.Name != "requestUnderstanding")
                        .ToDictionary(p => p.Name, p => p.Value.Clone()));
                }
                var id = function.TryGetProperty("id", out var identifier) ? identifier.GetString() : null;
                calls.Add(new(id ?? Guid.NewGuid().ToString("N"), name, args, ProviderId: id));
            }
            if (calls.Count is 0 or > 6 || content.GetRawText().Length > 150000) throw new JsonException("Invalid calls");
            return new(content, calls.ToArray(), understanding);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new AuthException("AGENT_AI_TIMEOUT", "AI phản hồi quá lâu ở bước này. Lịch chưa thay đổi.", 503); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or HttpRequestException)
        { throw new AuthException("AGENT_AI_INVALID_RESPONSE", "AI chưa trả về hành động hợp lệ. Lịch chưa thay đổi.", 503); }
    }
}
