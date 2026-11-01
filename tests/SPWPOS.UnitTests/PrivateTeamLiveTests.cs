using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class PrivateTeamLiveTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_AGENT_TESTS") == "1";
    [Fact(SkipUnless = nameof(Enabled), Skip = "Opt-in provider test with fictional data only.")]
    public async Task Provider_drafts_shared_task_and_routine_without_private_calendar_context()
    {
        var ct = TestContext.Current.CancellationToken; var root = Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src", "SPWPOS.Api", "appsettings.json")).AddUserSecrets("SPWPOS-Authentication-Development").Build();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(65) }; var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        await using var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(model)); var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope(); var teams = scope.ServiceProvider.GetRequiredService<IPrivateTeamService>(); var team = await teams.CreateTeamAsync(user.Id, new("Nhóm thử nghiệm tạm"), ct);
        var deadline = TimeZoneInfo.ConvertTime(h.Clock.GetUtcNow(), PlanningEngine.Zone("Asia/Ho_Chi_Minh")).AddDays(2).Date.AddHours(21);
        var draft = await teams.DraftTeamTaskAsync(user.Id, team, new($"Viết một trang mô tả endpoint GET /health và một ví dụ JSON cho nhóm, hạn chót 21:00 ngày {deadline:dd/MM/yyyy}. Tôi chưa biết cần bao lâu; hãy đề xuất thời lượng để tôi xem và xác nhận."), ct);
        Assert.NotNull(draft.Task); Assert.True(draft.Task.EstimatedMinutes > 0); Assert.Equal(new DateTimeOffset(deadline, TimeSpan.FromHours(7)), draft.Task.Deadline);
        Assert.Empty((await teams.GetTeamAsync(user.Id, team, ct)).Tasks);
        await teams.ConfirmTeamTaskDraftAsync(user.Id, team, draft.ProposalId, ct);
        var workspace = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await workspace.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 480, 1320)).ToArray()), ct);
        await workspace.SaveEventAsync(user.Id, null, new("Lịch học giả lập", h.Clock.GetUtcNow().AddHours(1), h.Clock.GetUtcNow().AddHours(2), EventType: "CLASS"), ct);
        var claim = await teams.PreviewTeamClaimAsync(user.Id, draft.Task.Id, ct); Assert.NotEmpty(claim.Options); Assert.True(claim.Analysis.Length >= 80);
        var policy = await model.DraftPolicyAsync("Sinh hoạt 08:00–22:00, nghỉ trưa 12:00–13:00, nghỉ tối 18:30–19:15, di chuyển 20 phút, khoảng tập trung tối thiểu 60 phút.", new(420, 1350, 705, 765, 1080, 1140, 30, 45, false), ct);
        Assert.Equal(new PolicyView(480, 1320, 720, 780, 1110, 1155, 20, 60, false), policy);
    }
}
