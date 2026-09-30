using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CarteiraFinanceira.Api.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(_ => { }).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
    }

    [Fact]
    public async Task Health_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
