using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
using Microsoft.Extensions.DependencyInjection;
namespace SPWPOS.IntegrationTests;

public sealed class WorkspaceFlowTests
{
    [Fact] public async Task Workspace_service_initializes_for_new_account()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var session = await AuthFlowTests.RegisterLoginAsync(factory, client);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        var db = scope.ServiceProvider.GetRequiredService<SPWPOS.Infrastructure.Persistence.ApplicationDbContext>();
        var uid = db.Users.Single().Id;
        var workspace = await service.GetAsync(uid, Ct);
        Assert.Empty(workspace.Tasks);
    }
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path); if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, Ct); var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"{path}: expected {expected}, got {response.StatusCode}: {text}"); return JsonDocument.Parse(text).RootElement.Clone();
    }
    internal static async Task Authorize(ApiFactory factory, HttpClient client)
    { var session = await AuthFlowTests.RegisterLoginAsync(factory, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Access); }
    internal static async Task<Guid> Create(HttpClient client, DateTimeOffset now, string name = "Build API") =>
        (await Send(client, HttpMethod.Post, "/api/tasks", new TaskInput(name, "", now.AddDays(1), 60))).GetProperty("data").GetProperty("id").GetGuid();
    internal static async Task SetupTime(HttpClient client) => _ = await Send(client, HttpMethod.Put, "/api/time/availability",
        new WeeklyInput(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()));
    [Fact] public async Task Create_plan_apply_execute_observe_and_scenario_does_not_persist()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        var now = factory.Clock.GetUtcNow(); var id = await Create(client, now);
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 0, 1440));
        var proposal = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(now, 1))).GetProperty("data");
        Assert.Empty(proposal.GetProperty("warnings").EnumerateArray());
        var planId = proposal.GetProperty("id").GetGuid(); await Send(client, HttpMethod.Post, $"/api/plans/{planId}/apply");
        await Send(client, HttpMethod.Post, $"/api/plans/{planId}/apply", expected: HttpStatusCode.Conflict);
        var sid = proposal.GetProperty("sessions")[0].GetProperty("id").GetGuid();
        factory.Clock.Advance(proposal.GetProperty("sessions")[0].GetProperty("start").GetDateTimeOffset() - factory.Clock.GetUtcNow());
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/start"); factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/pause"); factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/resume"); factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/complete");
        await Send(client, HttpMethod.Patch, $"/api/tasks/{id}/progress", new ProgressInput(100));
        var state = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(30, state.GetProperty("sessions")[0].GetProperty("actualMinutes").GetInt32());
        var scenario = new ScenarioInput(new(factory.Clock.GetUtcNow(), 2), new("What if", "", now.AddDays(2), 120));
        await Send(client, HttpMethod.Post, "/api/plans/scenario", scenario);
        var after = (await Send(client, HttpMethod.Get, "/api/tasks")).GetProperty("data"); Assert.Single(after.EnumerateArray());
    }
    [Fact] public async Task Dependency_cycles_and_stale_plans_are_rejected()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        var now = factory.Clock.GetUtcNow(); var a = await Create(client, now, "A"); var b = await Create(client, now, "B");
        await Send(client, HttpMethod.Post, $"/api/tasks/{b}/dependencies/{a}");
        await Send(client, HttpMethod.Post, $"/api/tasks/{a}/dependencies/{b}", expected: HttpStatusCode.Conflict);
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(now, 1))).GetProperty("data");
        await Create(client, now, "C"); await Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply", expected: HttpStatusCode.Conflict);
    }
    [Fact] public async Task Manual_move_keeps_breaks_and_split_preserves_work_duration_with_a_break()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        var now = factory.Clock.GetUtcNow();
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 15, 0, 1440));
        await Create(client, now, "A"); await Create(client, now, "B");
        // Keep the test away from a midnight boundary without changing JWT time.
        var morning = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(morning, 1))).GetProperty("data");
        await Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        var sessions = plan.GetProperty("sessions").EnumerateArray().OrderBy(x => x.GetProperty("start").GetDateTimeOffset()).ToArray();
        var firstId = sessions[0].GetProperty("id").GetGuid(); var secondId = sessions[1].GetProperty("id").GetGuid();
        var firstEnd = sessions[0].GetProperty("end").GetDateTimeOffset();
        var error = await Send(client, HttpMethod.Put, $"/api/sessions/{secondId}", new SessionEdit(firstEnd, firstEnd.AddHours(1)), HttpStatusCode.Conflict);
        Assert.Equal("SESSION_BREAK_CONFLICT", error.GetProperty("error").GetProperty("code").GetString());
        // Move the following task out of the way before splitting the first session.
        await Send(client, HttpMethod.Put, $"/api/sessions/{secondId}", new SessionEdit(firstEnd.AddHours(2), firstEnd.AddHours(3)));
        await Send(client, HttpMethod.Post, $"/api/sessions/{firstId}/split", new SplitInput(30));
        var firstTaskId = sessions[0].GetProperty("taskId").GetGuid();
        var split = (await Send(client, HttpMethod.Get, "/api/time/calendar")).GetProperty("data").GetProperty("sessions").EnumerateArray()
            .Where(x => x.GetProperty("taskId").GetGuid() == firstTaskId).OrderBy(x => x.GetProperty("start").GetDateTimeOffset()).ToArray();
        Assert.Equal(2, split.Length);
        Assert.Equal(15, (split[1].GetProperty("start").GetDateTimeOffset() - split[0].GetProperty("end").GetDateTimeOffset()).TotalMinutes);
        Assert.Equal(60, split.Sum(x => (x.GetProperty("end").GetDateTimeOffset() - x.GetProperty("start").GetDateTimeOffset()).TotalMinutes));
    }
    [Fact] public async Task Cross_account_resource_access_and_admin_access_are_denied()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var stranger = Guid.NewGuid(); await Send(client, HttpMethod.Put, $"/api/tasks/{stranger}", new TaskInput("Private", "", factory.Clock.GetUtcNow().AddDays(1), 60), HttpStatusCode.NotFound);
        await Send(client, HttpMethod.Get, "/api/admin/users", expected: HttpStatusCode.Forbidden);
        await Send(client, HttpMethod.Get, $"/api/plans/{stranger}", expected: HttpStatusCode.NotFound);
    }
    [Fact] public async Task Invalid_time_window_and_unknown_timezone_are_rejected()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        await Send(client, HttpMethod.Put, "/api/time/availability", new WeeklyInput([new(1, 600, 480)]), HttpStatusCode.BadRequest);
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("Unknown/Zone", 25, 90, 10, 480, 1080), HttpStatusCode.BadRequest);
    }
    [Fact] public async Task Browser_session_requires_csrf_header_and_does_not_expose_tokens()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); client.DefaultRequestHeaders.Authorization = null;
        var body = new { email = ServiceHarness.Address, password = ServiceHarness.Password };
        await Send(client, HttpMethod.Post, "/api/browser-session/login", body, HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Add("X-SPWPOS", "1");
        var result = await Send(client, HttpMethod.Post, "/api/browser-session/login", body);
        Assert.DoesNotContain("accessToken", result.ToString()); Assert.DoesNotContain("refreshToken", result.ToString());
        await Send(client, HttpMethod.Get, "/api/browser-session/me");
        client.DefaultRequestHeaders.Remove("X-SPWPOS");
        await Send(client, HttpMethod.Post, "/api/tasks", new TaskInput("Blocked", "", factory.Clock.GetUtcNow().AddDays(1), 60), HttpStatusCode.Forbidden);
    }
    [Fact] public async Task Real_cross_user_task_and_category_references_are_denied()
    {
        await using var factory = new ApiFactory(); using var owner = await factory.ClientAsync(); await Authorize(factory, owner);
        var task = await Create(owner, factory.Clock.GetUtcNow());
        var category = (await Send(owner, HttpMethod.Post, "/api/categories", new CategoryInput("Private"))).GetProperty("data").GetProperty("id").GetGuid();
        using var other = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        const string email = "other@example.com";
        await Send(other, HttpMethod.Post, "/api/auth/register", new { userName = "other.member", email, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "Other", lastName = "User" });
        await Send(other, HttpMethod.Post, "/api/auth/verify-email", new { email, otp = factory.Email.Inbox[(email, SPWPOS.Domain.Enums.OtpPurpose.EmailVerification)] });
        var login = (await Send(other, HttpMethod.Post, "/api/auth/login", new { email, password = ServiceHarness.Password })).GetProperty("data");
        other.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        await Send(other, HttpMethod.Get, $"/api/tasks/{task}", expected: HttpStatusCode.NotFound);
        await Send(other, HttpMethod.Delete, $"/api/tasks/{task}", expected: HttpStatusCode.NotFound);
        await Send(other, HttpMethod.Post, "/api/tasks", new TaskInput("Cross owner", "", factory.Clock.GetUtcNow().AddDays(1), 60, CategoryId: category), HttpStatusCode.NotFound);
        Assert.Single((await Send(owner, HttpMethod.Get, "/api/tasks")).GetProperty("data").EnumerateArray());
    }
    [Fact] public async Task Proposal_can_be_applied_after_review_and_unlocked_conflicting_work_is_rescheduled()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 0, 1440));
        await Create(client, factory.Clock.GetUtcNow());
        var proposal = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data");
        factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await Send(client, HttpMethod.Post, $"/api/plans/{proposal.GetProperty("id").GetGuid()}/apply");
        var s = proposal.GetProperty("sessions")[0]; var sid = s.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/lock");
        await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Conflict", s.GetProperty("start").GetDateTimeOffset(), s.GetProperty("end").GetDateTimeOffset()), HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/unlock");
        await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Meeting", s.GetProperty("start").GetDateTimeOffset(), s.GetProperty("end").GetDateTimeOffset()));
        var conflicts = (await Send(client, HttpMethod.Post, "/api/plans/conflicts", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data");
        Assert.Empty(conflicts.EnumerateArray());
        var workspace = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var replacement = Assert.Single(workspace.GetProperty("sessions").EnumerateArray());
        Assert.NotEqual(sid, replacement.GetProperty("id").GetGuid());
        Assert.False(replacement.GetProperty("start").GetDateTimeOffset() < s.GetProperty("end").GetDateTimeOffset() &&
            s.GetProperty("start").GetDateTimeOffset() < replacement.GetProperty("end").GetDateTimeOffset());
        var rejected = await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/start", expected: HttpStatusCode.BadRequest);
        Assert.Equal("INVALID_TRANSITION", rejected.GetProperty("error").GetProperty("code").GetString());
    }
    [Fact] public async Task Natural_language_duration_is_bounded_and_does_not_save_without_confirmation()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var result = (await Send(client, HttpMethod.Post, "/api/tasks/capture", new CaptureInput("Viết báo cáo ngày mai khoảng 3 tiếng"))).GetProperty("data");
        Assert.Equal(180, result.GetProperty("task").GetProperty("estimatedMinutes").GetInt32());
        await Send(client, HttpMethod.Post, "/api/tasks/capture", new CaptureInput("Task " + new string('9', 500) + " hours"));
        Assert.Empty((await Send(client, HttpMethod.Get, "/api/tasks")).GetProperty("data").EnumerateArray());
    }
}
