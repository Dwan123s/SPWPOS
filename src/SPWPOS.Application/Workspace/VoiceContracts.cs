namespace SPWPOS.Application.Workspace;

public sealed record VoiceTranscription(string Text);
public static class VoiceLimits
{
    public const int MaxAudioBytes = 2 * 1024 * 1024;
    public static bool ValidAudio(ReadOnlySpan<byte> audio, string mimeType) => mimeType switch
    {
        "audio/webm" => audio.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
        "audio/ogg" => audio.StartsWith("OggS"u8),
        "audio/mp4" => audio.Length >= 12 && audio.Slice(4, 4).SequenceEqual("ftyp"u8),
        "audio/wav" => audio.Length >= 12 && audio.StartsWith("RIFF"u8) && audio.Slice(8, 4).SequenceEqual("WAVE"u8),
        _ => false
    };
}
public interface IAgentVoiceTranscriber
{
    bool VoiceAvailable { get; }
    Task<VoiceTranscription> TranscribeAsync(byte[] audio, string mimeType, CancellationToken ct);
}
