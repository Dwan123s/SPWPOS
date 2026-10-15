using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;
public sealed record RecoverySettingInput(bool Enabled = true, bool AutoApply = false, [Range(1,14)] int MissedDays = 2,
    [Range(0,1439)] int QuietStartMinute = 1320, [Range(0,1439)] int QuietEndMinute = 420, Guid Version = default);
public sealed record RecoveryView(Guid Id, string Status, string Message, Guid[] TaskIds, PlanView? Plan, DateTimeOffset CreatedAt);
public sealed record RecoveryPage(RecoverySettingInput Settings, RecoveryView[] Items, int Page, int Total);
