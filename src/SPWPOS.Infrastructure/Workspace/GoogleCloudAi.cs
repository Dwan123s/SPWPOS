using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using SPWPOS.Application.Authentication;

namespace SPWPOS.Infrastructure.Workspace;

public interface IVertexAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct);
}

internal sealed class VertexAccessTokenProvider : IVertexAccessTokenProvider
{
    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var credential = (await GoogleCredential.GetApplicationDefaultAsync(ct))
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct);
    }
}

public sealed partial class TaskInterpreter
{
    private string VertexProject => (config["AiSettings:VertexProjectId"] ?? "").Trim();
    private string VertexLocation => (config["AiSettings:VertexLocation"] ?? "global").Trim().ToLowerInvariant();
    private string VertexAuthentication => (config["AiSettings:VertexAuthentication"] ?? "ApiKey").Trim().ToUpperInvariant();
    // API-key calls always use the publisher route, including older configs with a false flag.
    private bool VertexExpress => VertexAuthentication == "APIKEY";
    private bool ValidVertexConfiguration =>
        Regex.IsMatch(VertexProject, @"^[a-z][a-z0-9-]{4,28}[a-z0-9]$", RegexOptions.None, TimeSpan.FromMilliseconds(100)) &&
        Regex.IsMatch(VertexLocation, @"^(?:global|[a-z]+(?:-[a-z]+)+[0-9])$", RegexOptions.None, TimeSpan.FromMilliseconds(100)) &&
        (VertexExpress ? VertexLocation == "global" && !string.IsNullOrWhiteSpace(ApiKey)
            : VertexAuthentication == "ADC");

    private string AiEndpoint(string model, bool stream = false)
    {
        var modelId = Uri.EscapeDataString(model.StartsWith("models/", StringComparison.Ordinal) ? model[7..] : model);
        if (Provider == "OpenAI") return "https://api.openai.com/v1/responses";
        var method = stream ? "streamGenerateContent" : "generateContent";
        var sse = stream ? "?alt=sse" : "";
        if (Provider == "Gemini") return $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:{method}{sse}";
        // The documented API-key route resolves project/billing from the key.
        // Project/location resource paths are used by the ADC route.
        if (VertexExpress) return $"https://aiplatform.googleapis.com/v1/publishers/google/models/{modelId}:{method}?key={Uri.EscapeDataString(ApiKey ?? "")}{(stream ? "&alt=sse" : "")}";
        var host = VertexLocation == "global" ? "aiplatform.googleapis.com" : $"{VertexLocation}-aiplatform.googleapis.com";
        return $"https://{host}/v1/projects/{VertexProject}/locations/{VertexLocation}/publishers/google/models/{modelId}:{method}{sse}";
    }

    private async Task AuthenticateGoogleAsync(HttpRequestMessage request, string errorPrefix, CancellationToken ct)
    {
        if (Provider != "VertexAI" || VertexAuthentication == "APIKEY")
        {
            request.Headers.Add("x-goog-api-key", ApiKey);
            return;
        }
        try
        {
            var token = await (vertexTokens ?? new VertexAccessTokenProvider()).GetTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Missing token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("x-goog-user-project", VertexProject);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Credential exceptions can contain file paths, account details or token responses.
            throw new AuthException($"{errorPrefix}_AI_AUTH_FAILED",
                "Chưa xác thực được Google Cloud. Thiết lập Application Default Credentials và quyền Agent Platform User cho project trên server; lịch chưa thay đổi.", 503);
        }
    }

    private HttpContent GoogleContent(string instructions, string prompt, JsonElement schema, int maxTokens, bool includeStore = false, object[]? contentParts = null)
    {
        // Gemini 3.8 counts thinking and the structured answer in the same
        // output budget. The earlier 6,000-token cap truncated real agent turns.
        var outputLimit = UsesGemini38 ? Math.Max(maxTokens, 16384) : maxTokens;
        // Vertex v1 uses responseJsonSchema. Developer API uses responseFormat.
        var generation = new Dictionary<string, object> { ["maxOutputTokens"] = outputLimit };
        if (Provider == "VertexAI")
        {
            generation["responseMimeType"] = "application/json";
            generation["responseJsonSchema"] = schema;
        }
        else generation["responseFormat"] = new { text = new { mimeType = "APPLICATION_JSON", schema } };
        // NORA extracts structured intent; deterministic planning happens on
        // the server. Avoid unbounded deliberation over feasibility in the model.
        if (UsesGemini38) generation["thinkingConfig"] = new { thinkingLevel = "LOW" };
        var body = new Dictionary<string, object> {
            ["systemInstruction"] = new { parts = new[] { new { text = instructions } } },
            ["contents"] = new[] { new { role = "user", parts = contentParts ?? [new { text = prompt }] } },
            ["generationConfig"] = generation
        };
        if (includeStore && Provider == "Gemini") body["store"] = false;
        return JsonContent.Create(body);
    }

    private bool UsesGemini38 => IsGoogle && (config["AiSettings:Model"] is "gemini-3.8-flash" or "models/gemini-3.8-flash");

    private async Task<AuthException> GoogleFailureDetailsAsync(HttpResponseMessage response, AuthException failure, CancellationToken ct)
    {
        if (Provider != "VertexAI") return failure;
        failure.Data["GoogleHttpStatus"] = (int)response.StatusCode;
        failure.Data["GoogleReason"] = "UNKNOWN";
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!body.RootElement.TryGetProperty("error", out var error)) return failure;
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.ValueKind != JsonValueKind.Object || !detail.TryGetProperty("@type", out var type) ||
                        type.GetString() != "type.googleapis.com/google.rpc.ErrorInfo" || !detail.TryGetProperty("reason", out var reason) ||
                        reason.ValueKind != JsonValueKind.String) continue;
                    var code = reason.GetString();
                    if (code is "SERVICE_DISABLED" or "BILLING_DISABLED" or "API_KEY_INVALID" or "API_KEY_SERVICE_BLOCKED" or
                        "API_KEY_HTTP_REFERRER_BLOCKED" or "API_KEY_IP_ADDRESS_BLOCKED" or "API_KEY_NOT_FOUND" or
                        "API_KEY_RESTRICTION_MISSING" or "API_KEY_NOT_SUPPORTED" or "IAM_PERMISSION_DENIED" or
                        "ACCOUNT_STATE_INVALID" or "SECURITY_POLICY_VIOLATED" or "CONSUMER_INVALID" or "PROJECT_INVALID")
                    {
                        failure.Data["GoogleReason"] = code;
                        return failure;
                    }
                }
            if (error.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
                status.GetString() is "UNAUTHENTICATED" or "PERMISSION_DENIED" or "INVALID_ARGUMENT" or "NOT_FOUND" or "RESOURCE_EXHAUSTED")
                failure.Data["GoogleReason"] = status.GetString();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or HttpRequestException) { }
        // Never retain message/metadata/body: they can echo credentials or user data.
        return failure;
    }
}
