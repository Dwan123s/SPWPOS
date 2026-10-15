using System.Text;
using System.Text.Json;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class TaskInterpreter
{
    private async Task<string> ReadGoogleAgentStreamAsync(HttpResponseMessage response,
        Func<string, CancellationToken, Task> onDelta, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var output = new StringBuilder();
        var data = new StringBuilder();
        var emitted = "";
        string? finish = null;
        var total = 0;
        async Task ReadEvent()
        {
            if (data.Length == 0) return;
            var value = data.ToString(); data.Clear();
            if (value.Trim() == "[DONE]") return;
            using var chunk = JsonDocument.Parse(value);
            usageCollector?.Read(chunk.RootElement);
            if (chunk.RootElement.TryGetProperty("error", out _)) throw new JsonException("Provider stream failed");
            if (!chunk.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return;
            var candidate = candidates[0];
            if (candidate.TryGetProperty("finishReason", out var reason)) finish = reason.GetString();
            if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                foreach (var part in parts.EnumerateArray())
                {
                    // Never send model thoughts, signatures, raw JSON commands or provider metadata to the browser.
                    if (part.TryGetProperty("thought", out var thought) && thought.GetBoolean()) continue;
                    if (part.TryGetProperty("text", out var text)) output.Append(text.GetString());
                }
            if (output.Length > 131072) throw new JsonException("Agent output too large");
            var prefix = PublicReplyPrefix(output.ToString());
            if (!prefix.StartsWith(emitted, StringComparison.Ordinal)) throw new JsonException("Invalid reply stream");
            if (prefix.Length > emitted.Length)
            {
                await onDelta(prefix[emitted.Length..], ct);
                emitted = prefix;
            }
        }
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            total += line.Length;
            if (total > 1048576 || line.Length > 131072) throw new JsonException("Provider stream too large");
            if (line.Length == 0) await ReadEvent();
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line.AsSpan(5).TrimStart());
                if (data.Length > 131072) throw new JsonException("Provider event too large");
            }
        }
        await ReadEvent();
        if (finish != "STOP") throw new JsonException("Incomplete provider stream");
        return output.ToString();
    }

    // Read only the top-level reply string, allowing incomplete JSON and split escapes.
    private static string PublicReplyPrefix(string json)
    {
        var depth = 0;
        for (var index = 0; index < json.Length; index++)
        {
            var c = json[index];
            if (c is '{' or '[') { depth++; continue; }
            if (c is '}' or ']') { depth--; continue; }
            if (c != '"') continue;
            var start = index;
            while (++index < json.Length)
            {
                if (json[index] == '\\') { index++; continue; }
                if (json[index] == '"') break;
            }
            if (index >= json.Length) return "";
            if (depth != 1 || json[start..(index + 1)] != "\"reply\"") continue;
            var cursor = index + 1;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
            if (cursor >= json.Length || json[cursor++] != ':') continue;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
            if (cursor >= json.Length || json[cursor++] != '"') return "";
            var decoded = new StringBuilder();
            while (cursor < json.Length)
            {
                c = json[cursor++];
                if (c == '"') break;
                if (c != '\\') { if (c < 32) throw new JsonException("Invalid string"); decoded.Append(c); continue; }
                if (cursor >= json.Length) break;
                c = json[cursor++];
                if (c == 'u')
                {
                    if (cursor + 4 > json.Length) break;
                    if (!ushort.TryParse(json.AsSpan(cursor, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code)) throw new JsonException("Invalid unicode escape");
                    decoded.Append((char)code); cursor += 4;
                }
                else decoded.Append(c switch { '"' => '"', '\\' => '\\', '/' => '/', 'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r', 't' => '\t', _ => throw new JsonException("Invalid escape") });
            }
            if (decoded.Length > 0 && char.IsHighSurrogate(decoded[^1])) decoded.Length--;
            return decoded.ToString();
        }
        return "";
    }
}
