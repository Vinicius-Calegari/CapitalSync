using System.Text.Json.Serialization;

namespace CarteiraFinanceira.Api.Models.Api;

public class TwelveDataSymbolSearchResponse
{
    [JsonPropertyName("data")]
    public List<TwelveDataSymbolSearchItem> Data { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int? Code { get; set; }
}

public class TwelveDataSymbolSearchItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
