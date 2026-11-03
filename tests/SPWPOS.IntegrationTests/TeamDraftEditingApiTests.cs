using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamDraftEditingApiTests
{
    private sealed class Factory(ITaskInterpreter? actual = null) : ApiFactory {
        public AgentAiTestDouble Ai { get; } = new() {BriefResponse=c=>new("Proposal","Bạn chốt giờ nộp nhé?","Một gói cho một thành viên",[
            new("Xây dựng proposal","Tóm tắt",["Làm rõ phạm vi"],["Proposal"],["Leader duyệt"],[],[],360,null,"")],[])};
        protected override void ConfigureWebHost(IWebHostBuilder builder) {base.ConfigureWebHost(builder);builder.ConfigureTestServices(s=>s.AddSingleton<ITaskInterpreter>(actual ?? Ai));}
    }
    public static bool LiveWordEnabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_WORD_API") == "1";
    [Fact(SkipUnless=nameof(LiveWordEnabled),Skip="Opt-in Gemini 3.8 HTTP regression with generated example Word and an isolated in-memory database.")]
    public async Task Word_upload_returns_two_editable_packages_over_http_with_configured_flash()
    {
        var ct=TestContext.Current.CancellationToken;
        var root=Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(root,"src/SPWPOS.Api/appsettings.json")).AddUserSecrets("SPWPOS-Authentication-Development").Build();
        Assert.Equal("gemini-3.8-flash",config["AiSettings:Model"]);
        using var providerHttp=new HttpClient();var model=new TaskInterpreter(providerHttp,config,NullLogger<TaskInterpreter>.Instance);
        // JWT lifetime validation uses the host clock; don't shift the API clock
        // ahead of the validator. The example's 04/11 deadline is still future.
        await using var f=new Factory(model);
        using var client=await f.ClientAsync();var tokens=await AuthFlowTests.RegisterLoginAsync(f,client);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Access);
        var created=await client.PostAsJsonAsync("/api/teams",new{name="Fictional Word regression"},ct);created.EnsureSuccessStatusCode();
        using var group=JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));var team=group.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        await using(var scope=f.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var member=new ApplicationUser {Id=Guid.NewGuid(),UserName="word.api.member",NormalizedUserName="WORD.API.MEMBER",Email="word.api.member@example.com",NormalizedEmail="WORD.API.MEMBER@EXAMPLE.COM",FirstName="Fake",LastName="Member",EmailConfirmed=true,IsActive=true};
            db.Users.Add(member);db.TeamMemberships.Add(new(){TeamId=team,UserId=member.Id});await db.SaveChangesAsync(ct);
        }
        var file=new TeamBriefFile("SSG105-example.docx","application/vnd.openxmlformats-officedocument.wordprocessingml.document",Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(root,"outputs/SSG105_Ke_hoach_xay_dung_proposal.docx"),ct)));
        var response=await client.PostAsJsonAsync($"/api/teams/{team}/briefs",new TeamBriefInput("",file),ct);
        response.EnsureSuccessStatusCode();using var parsed=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var draft=parsed.RootElement.GetProperty("data");
        Assert.Equal(2,draft.GetProperty("memberCount").GetInt32());Assert.NotEmpty(draft.GetProperty("question").GetString()!);
        var packages=draft.GetProperty("pendingTasks").EnumerateArray().ToArray();Assert.Equal(2,packages.Length);
        Assert.All(packages,t=>{Assert.Contains("TÓM TẮT",t.GetProperty("description").GetString());Assert.Contains("ĐẦU RA CẦN BÀN GIAO",t.GetProperty("description").GetString());});
        var id=draft.GetProperty("proposalId").GetGuid();var revision=draft.GetProperty("revision").GetGuid();
        var edits=packages.Select(t=>new TeamDraftTaskEdit(t.GetProperty("id").GetGuid(),t.GetProperty("name").GetString()!,t.GetProperty("description").GetString()!,120,DateTimeOffset.Parse("2026-11-04T21:00:00+07:00"))).ToArray();
        var save=await client.PostAsJsonAsync($"/api/teams/{team}/briefs/{id}/edit",new TeamDraftEditInput(revision,edits),ct);save.EnsureSuccessStatusCode();
        using var saved=JsonDocument.Parse(await save.Content.ReadAsStringAsync(ct));Assert.Equal(2,saved.RootElement.GetProperty("data").GetProperty("tasks").GetArrayLength());
        await using(var scope=f.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Assert.Equal(0,await db.TeamTasks.CountAsync(ct));Assert.Equal(0,await db.WorkSessions.CountAsync(ct));}
        Console.WriteLine("LIVE Word HTTP: Gemini 3.8 Flash, 2 pending packages, edit returns 2 ready packages, no published tasks or real-account writes.");
    }
    [Fact] public async Task Pending_brief_can_be_edited_then_published_only_with_the_reviewed_revision()
    {
        var ct=TestContext.Current.CancellationToken;await using var f=new Factory();using var client=await f.ClientAsync();
        var tokens=await AuthFlowTests.RegisterLoginAsync(f,client);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Access);
        var created=await client.PostAsJsonAsync("/api/teams",new{name="Draft editor"},ct);created.EnsureSuccessStatusCode();
        using var group=JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));var team=group.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var response=await client.PostAsJsonAsync($"/api/teams/{team}/briefs",new TeamBriefInput("Xây dựng proposal, chưa chốt giờ"),ct);response.EnsureSuccessStatusCode();
        using var parsed=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var draft=parsed.RootElement.GetProperty("data");var proposal=draft.GetProperty("proposalId").GetGuid();
        var task=Assert.Single(draft.GetProperty("pendingTasks").EnumerateArray());var revision=draft.GetProperty("revision").GetGuid();
        Assert.NotEqual(Guid.Empty,proposal);Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/teams/{team}/briefs/{proposal}/confirm",new{},ct)).StatusCode);
        var edit=new TeamDraftEditInput(revision,[new(task.GetProperty("id").GetGuid(),"Proposal đã chỉnh","TÓM TẮT\nNội dung leader sửa\nYÊU CẦU CẦN THỰC HIỆN\n• Khảo sát người dùng",120,f.Clock.GetUtcNow().AddDays(2))]);
        var saved=await client.PostAsJsonAsync($"/api/teams/{team}/briefs/{proposal}/edit",edit,ct);saved.EnsureSuccessStatusCode();
        using var changed=JsonDocument.Parse(await saved.Content.ReadAsStringAsync(ct));var updated=changed.RootElement.GetProperty("data");var next=updated.GetProperty("revision").GetGuid();
        Assert.NotEqual(revision,next);Assert.Single(updated.GetProperty("tasks").EnumerateArray());Assert.Equal(1,f.Ai.Calls);
        await using(var scope=f.Services.CreateAsyncScope())Assert.Equal(0,await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeamTasks.CountAsync(ct));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/teams/{team}/briefs/{proposal}/edit",edit,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/teams/{team}/briefs/{proposal}/confirm",new{},ct)).StatusCode);
        var confirm=await client.PostAsJsonAsync($"/api/teams/{team}/briefs/{proposal}/confirm",new TeamBriefConfirmInput(Revision:next),ct);confirm.EnsureSuccessStatusCode();
        await using(var scope=f.Services.CreateAsyncScope()) {var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var published=await db.TeamTasks.SingleAsync(ct);
            Assert.Equal("Proposal đã chỉnh",published.Name);Assert.Equal(120,published.EstimatedMinutes);Assert.Contains("Nội dung leader sửa",published.Description);Assert.Equal(0,await db.WorkSessions.CountAsync(ct));}
    }
}
