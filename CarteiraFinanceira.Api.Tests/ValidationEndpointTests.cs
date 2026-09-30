using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CarteiraFinanceira.Api.Tests;

public class ValidationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ValidationEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/ativos/consultar?ativo=")]
    [InlineData("/api/ativos/multiplas?ativos=")]
    public async Task EmptyAssetInput_ReturnsBadRequest(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Suggestions_ReturnsSuccessWithoutExternalProvider()
    {
        var response = await _client.GetAsync("/api/ativos/sugestoes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
