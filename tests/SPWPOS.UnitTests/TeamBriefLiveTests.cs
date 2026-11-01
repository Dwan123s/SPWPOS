using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class TeamBriefLiveTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_TEAM_BRIEF") == "1";
    [Fact(SkipUnless = nameof(Enabled), Skip = "Opt-in real Gemini PDF brief check with fictional data only.")]
    public async Task Configured_flash_reads_real_pdf_and_builds_complete_member_sized_draft_without_saving_tasks()
    {
        var ct = TestContext.Current.CancellationToken; var root = Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src/SPWPOS.Api/appsettings.json"))
            .AddUserSecrets("SPWPOS-Authentication-Development").Build();
        Assert.Equal("gemini-3.8-flash", config["AiSettings:Model"]);
        using var http = new HttpClient(new Diagnostics(config)) { Timeout = TimeSpan.FromSeconds(65) };
        var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        await using var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(model));
        var leader = await h.RegisterAsync(); var memberId = Guid.NewGuid();
        Guid teamId;
        await using (var scope = h.Provider.CreateAsyncScope())
            teamId = await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().CreateTeamAsync(leader.Id, new("Fictional PDF test"), ct);
        await h.DbAsync(async db => {
            db.Users.Add(new() { Id = memberId, UserName = "fake.member", Email = "fake.member@example.com", NormalizedEmail = "FAKE.MEMBER@EXAMPLE.COM", FirstName = "Fake", LastName = "Member", IsActive = true, EmailConfirmed = true });
            db.TeamMemberships.Add(new() { UserId = memberId, TeamId = teamId }); await db.SaveChangesAsync(ct);
        });
        var due = h.Clock.GetUtcNow().AddDays(7).ToString("yyyy-MM-dd");
        var lines = new[] { "PROJECT BRIEF - fictional time manager app", "Project deadline: " + due + " at 21:00 Vietnam time (UTC+07:00).",
            "Requirements: build a REST API for creating and listing tasks.", "Each task has a title and a deadline. Deliver API source and API documentation.",
            "Also deliver automated tests for successful requests and invalid deadlines.", "Write a test report with steps, expected and actual results.",
            "Acceptance: API validates required title and rejects past deadlines.", "Acceptance: all automated tests pass and documentation explains endpoints.",
            "Coordinate API and tests. Use the same project deadline for all work." };
        var pdf = Pdf(lines);
        await using var request = h.Provider.CreateAsyncScope();
        TeamBriefDraft draft;
        try { draft = await request.ServiceProvider.GetRequiredService<IPrivateTeamService>().DraftTeamBriefAsync(leader.Id, teamId,
            new("Hãy đọc tài liệu và chia việc cho cả nhóm, bao gồm trưởng nhóm.", new("fictional-brief.pdf", "application/pdf", Convert.ToBase64String(pdf))), ct); }
        catch (SPWPOS.Application.Authentication.AuthException e) { throw new InvalidOperationException(Diagnostics.LastError ?? e.Code, e); }
        Assert.Empty(draft.Question); Assert.NotEqual(Guid.Empty, draft.ProposalId); Assert.Equal(2, draft.Tasks.Length);
        Assert.All(draft.Tasks, task => { Assert.Contains("ĐẦU RA CẦN BÀN GIAO", task.Description); Assert.Contains("TIÊU CHÍ HOÀN THÀNH", task.Description); Assert.True(task.Deadline > h.Clock.GetUtcNow()); });
        Assert.Equal(0, await h.DbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.TeamTasks, ct)));
        Console.WriteLine($"Live Gemini 3.8 PDF brief: {draft.Tasks.Length} complete work packages; deadline {due}; no published tasks.");
    }
    private static byte[] Pdf(string[] lines)
    {
        var content = "BT /F1 12 Tf 40 780 Td 18 TL " + string.Join(" ", lines.Select(line => "(" + line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ") Tj T*")) + " ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream" };
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = pdf.Length; pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
    private sealed class Diagnostics(IConfiguration config) : DelegatingHandler(new HttpClientHandler())
    {
        public static string? LastError { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var message = body.RootElement.GetProperty("error").GetRawText();
                foreach (var key in new[] { "AiSettings:VertexApiKey", "AiSettings:GeminiApiKey", "AiSettings:ApiKey" })
                    if (!string.IsNullOrEmpty(config[key])) message = message.Replace(config[key]!, "[redacted]", StringComparison.Ordinal);
                LastError = $"Fictional PDF provider HTTP {(int)response.StatusCode}: {message[..Math.Min(message.Length, 2500)]}";
            }
            return response;
        }
    }
}
