using System.Net;
using System.Text.RegularExpressions;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class StaticAssetPolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public async Task Hashed_assets_are_immutable_but_html_revalidates_and_private_api_never_caches()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var html = await client.GetAsync("/", Ct);
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.True(html.Headers.CacheControl?.NoCache);
        var body = await html.Content.ReadAsStringAsync(Ct);
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
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
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
