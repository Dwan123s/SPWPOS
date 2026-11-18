using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SPWPOS.TestSupport;

namespace SPWPOS.IntegrationTests;

[Collection("Cloud Run environment")]
public sealed class CloudRunIngressTests
{
    [Fact]
    public async Task Managed_tls_ingress_keeps_api_requests_https_after_transport_termination()
    {
        var previous = Environment.GetEnvironmentVariable("K_SERVICE");
        var previousSetting = Environment.GetEnvironmentVariable("Security__CloudRunDirectIngress");
        Environment.SetEnvironmentVariable("K_SERVICE", "planora-test");
        Environment.SetEnvironmentVariable("Security__CloudRunDirectIngress", "true");
        try
        {
            await using var factory = new IngressFactory();
            using var client = await factory.ClientAsync();
            client.BaseAddress = new Uri("http://localhost");
            using var response = await client.GetAsync("/api/integrations/google-calendar/status", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }
        finally
        {
            Environment.SetEnvironmentVariable("K_SERVICE", previous);
            Environment.SetEnvironmentVariable("Security__CloudRunDirectIngress", previousSetting);
        }
    }

    private sealed class IngressFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Security:CloudRunDirectIngress"] = "true" }));
        }
    }
}

[CollectionDefinition("Cloud Run environment", DisableParallelization = true)]
public sealed class CloudRunEnvironmentCollection;
