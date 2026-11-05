using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class ContinuousNoteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Provider : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var text = JsonSerializer.Serialize(new { activities = new[] { new {
                kind="TASK", name="Viết báo cáo", description="Làm liên tục", type="WRITING", start=(string?)null, end=(string?)null,
                estimatedMinutes=120, deadline="2026-11-05T18:00:00Z", priority=2, isHardDeadline=true, repeatWeeks=1, question="", splittable=false
            } }, needsReview=false, messages=Array.Empty<string>() });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                output = new[] {new { type="message", content=new[] { new { type="output_text", text } } } }
            })) });
        }
    }
    [Fact]
    public async Task Note_AI_keeps_explicit_continuity_in_its_structured_result()
    {
        using var http = new HttpClient(new Provider());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["AiSettings:Enabled"]="true", ["AiSettings:Provider"]="OpenAI", ["AiSettings:ApiKey"]="test-only", ["AiSettings:Model"]="test-model"
        }).Build();
        var result = await new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance).AnalyzeNoteAsync(
            "Viết báo cáo liên tục 120 phút, xong trước 18 giờ ngày 5/11/2026", true, "UTC", new(2026,11,4,8,0,0,TimeSpan.Zero), Ct);
        Assert.False(Assert.Single(result.Activities!).Splittable);
    }

    private sealed class Interpreter : ITaskInterpreter
    {
        public NoteAgentCapabilities Capabilities => new(true, Provider:"Gemini");
        public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string zone, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(new NoteAnalysis([], "Gemini", [], Activities:[new("TASK", "Viết báo cáo", Type:"WRITING", EstimatedMinutes:120,
                Deadline:new DateTimeOffset(now.UtcDateTime.Date.AddDays(1).AddHours(12),TimeSpan.Zero), IsHardDeadline:true, Splittable:false)]));
        public Task<TaskSuggestion> CaptureAsync(CaptureInput input,string zone,DateTimeOffset now,CancellationToken ct) => throw new NotSupportedException();
        public Task<BreakdownView> BreakdownAsync(TaskInput task,bool useAi,CancellationToken ct) => throw new NotSupportedException();
    }
    [Fact]
    public async Task Note_preview_cannot_drop_continuity_to_fill_separate_one_hour_gaps()
    {
        await using var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(new Interpreter()));
        var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await service.SetPreferenceAsync(user.Id, new("UTC",25,240,0,540,1080),Ct);
        await service.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0,7).SelectMany(day=>new[] {
            new AvailabilityInput(day,540,600),new AvailabilityInput(day,660,720)
        }).ToArray()),Ct);
        var note = await service.SaveNoteAsync(user.Id,null,new("Báo cáo","Viết báo cáo liên tục trong 120 phút"),Ct);
        note = await service.AnalyzeNoteAsync(user.Id,note.Id,new(note.Version),Ct);
        var plan = await service.PreviewNoteActivitiesAsync(user.Id,note.Id,new(note.Version,note.Analysis!.Activities!,ReviewedConditions:true),Ct);
        Assert.Empty(plan.Sessions);
        Assert.Contains(plan.Warnings,warning=>warning.UnscheduledMinutes==120);
    }
}
