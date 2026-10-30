using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class StaticAssetPolicyTests
{
    // Backend CI no longer builds React. Verify the API's legacy static-file
    // policy against a fixture, independently of generated frontend artifacts.
    private sealed class StaticFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            for (var root = new DirectoryInfo(AppContext.BaseDirectory); root is not null; root = root.Parent)
                if (File.Exists(Path.Combine(root.FullName, "SPWPOS.sln"))) {
                    var path = Path.Combine(root.FullName, "tests", "SPWPOS.IntegrationTests", "Fixtures", "static-web-root");
                    builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(_ => new FixtureWebRoot(path)));
                    return;
                }
            throw new InvalidOperationException("Cannot locate static policy fixture.");
        }
    }
    private sealed class FixtureWebRoot(string path) : IStartupFilter, IDisposable
    {
        private readonly PhysicalFileProvider files = new(path);
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
            // Override after the Development static-web-assets manifest loads;
            // otherwise a locally built React entry can shadow the fixture.
            var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            environment.WebRootPath = path; environment.WebRootFileProvider = files;
            next(app);
        };
        public void Dispose() => files.Dispose();
    }
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public async Task Hashed_assets_are_immutable_but_html_revalidates_and_private_api_never_caches()
    {
        await using var factory = new StaticFactory(); using var client = await factory.ClientAsync();
        using var html = await client.GetAsync("/", Ct);
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.True(html.Headers.CacheControl?.NoCache);
        var body = await html.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Backend policy test fixture", body);
        var css = Regex.Match(body, "href=\"([^\"]+\\.css)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value;
        Assert.StartsWith("/app/assets/", css);
        using var asset = await client.GetAsync(css, Ct);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.True(asset.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromDays(365), asset.Headers.CacheControl?.MaxAge);
        Assert.Contains("immutable", asset.Headers.CacheControl!.ToString());
        using var privateResponse = await client.GetAsync("/api/workspace", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, privateResponse.StatusCode);
        Assert.True(privateResponse.Headers.CacheControl?.NoStore);
    }
    [Fact] public async Task Public_text_assets_support_compression()
    {
        await using var factory = new StaticFactory(); using var client = await factory.ClientAsync();
        var html = await client.GetStringAsync("/", Ct);
        var css = Regex.Match(html, "href=\"([^\"]+\\.css)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Get, css);
        request.Headers.AcceptEncoding.ParseAdd("br, gzip");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Content.Headers.ContentEncoding, x => x is "br" or "gzip");
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
    }
}
