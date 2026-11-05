namespace SPWPOS.Application.Workspace;

public sealed record TimetableImageReading(string Text, string[] Warnings, bool NeedsReview);

public static class TimetableImageLimits
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxImages = 2;
    public const int MaxChatRequestBytes = 15 * 1024 * 1024;
    public const int MaxTextLength = 1600;
    public static bool ValidImage(ReadOnlySpan<byte> image, string mimeType) => mimeType switch
    {
        "image/png" => image.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => image.StartsWith(new byte[] { 255, 216, 255 }),
        "image/webp" => image.Length >= 12 && image.StartsWith("RIFF"u8) && image.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}

public interface IAgentTimetableImageReader
{
    bool ImageAvailable { get; }
    Task<TimetableImageReading> ReadTimetableAsync(byte[] image, string mimeType, CancellationToken ct);
}
