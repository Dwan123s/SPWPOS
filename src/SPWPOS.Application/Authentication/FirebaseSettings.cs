using System.Text.RegularExpressions;
namespace SPWPOS.Application.Authentication;

public sealed class FirebaseSettings
{
    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string AppId { get; set; } = "";
    public string ServiceAccountPath { get; set; } = "";
    public string AuthDomain => $"{ProjectId}.firebaseapp.com";
    public bool Valid => !Enabled || (Regex.IsMatch(ProjectId, "^[a-z][a-z0-9-]{4,62}$") && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(AppId));
}
