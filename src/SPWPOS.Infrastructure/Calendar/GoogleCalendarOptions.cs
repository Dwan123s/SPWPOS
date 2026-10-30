namespace SPWPOS.Infrastructure.Calendar;

public sealed class GoogleCalendarOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "https://localhost:7043/api/integrations/google-calendar/callback";
    public string WebhookUrl { get; set; } = "";
    public string KeyRingPath { get; set; } = "";
    public int PollSeconds { get; set; } = 60;
    public bool Configured => Enabled && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    public bool Valid => !Enabled || Configured && !string.IsNullOrWhiteSpace(KeyRingPath)
        && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var redirect) && redirect.Scheme == "https" && string.IsNullOrEmpty(redirect.Query)
        && (string.IsNullOrWhiteSpace(WebhookUrl) || Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var hook) && hook.Scheme == "https" && !hook.IsLoopback)
        && PollSeconds is >= 30 and <= 3600;
}
