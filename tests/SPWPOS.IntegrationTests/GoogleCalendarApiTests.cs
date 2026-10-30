using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Calendar;
using SPWPOS.Infrastructure.Calendar;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;

namespace SPWPOS.IntegrationTests;

public sealed class GoogleCalendarApiTests
{
    [Theory]
    [InlineData("missing_scope", "CALENDAR_SCOPE_REQUIRED")]
    [InlineData("invalid_grant", "CALENDAR_CODE_EXPIRED")]
    [InlineData("invalid_client", "CALENDAR_CLIENT_REJECTED")]
    [InlineData("private-canary", "CALENDAR_PROVIDER_ERROR")]
    public async Task Callback_returns_a_specific_safe_error_without_exposing_authorization_data(string reason, string expected)
    {
        await using var factory = new CallbackFactory(reason); using var client = await factory.ClientAsync();
        using var response = await client.GetAsync("/api/integrations/google-calendar/callback?state=test-state&code=private-code", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Equal("/?googleCalendar=" + expected + "#settings", location);
        Assert.DoesNotContain("private-code", location); Assert.DoesNotContain("private-canary", location);
    }
    [Fact] public async Task Connection_endpoints_require_authentication_and_disabled_status_is_explicit()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await Send(client, HttpMethod.Get, "/api/integrations/google-calendar/status", expected: HttpStatusCode.Unauthorized);
        await Send(client, HttpMethod.Post, "/api/integrations/google-calendar/connect", new { reminderMinutes = 15 }, HttpStatusCode.Unauthorized);
        await Authorize(factory, client);
        var result = (await Send(client, HttpMethod.Get, "/api/integrations/google-calendar/status")).GetProperty("data");
        Assert.False(result.GetProperty("configured").GetBoolean()); Assert.False(result.GetProperty("connected").GetBoolean());
        await Send(client, HttpMethod.Post, "/api/integrations/google-calendar/connect", new { reminderMinutes = 15 }, HttpStatusCode.ServiceUnavailable);
        using var response = await client.PostAsync("/api/integrations/google-calendar/webhook", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private sealed class CallbackFactory(string reason) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IGoogleCalendarService>(new CallbackService(reason)));
        }
    }
    private sealed class CallbackService(string reason) : IGoogleCalendarService
    {
        public Task CompleteOAuthAsync(string state, string? code, string? error, CancellationToken ct) =>
            Task.FromException(new CalendarProviderException(403, reason, "token"));
        public Task<CalendarConnectionView> StatusAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<CalendarConnectView> ConnectAsync(Guid userId, CalendarReminderInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task RequestSyncAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateRemindersAsync(Guid userId, CalendarReminderInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task DisconnectAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CalendarConflictView>> ConflictsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task ResolveAsync(Guid userId, Guid id, string keep, CancellationToken ct) => throw new NotSupportedException();
        public Task ReceiveWebhookAsync(string channelId, string resourceId, string token, CancellationToken ct) => throw new NotSupportedException();
    }
}
