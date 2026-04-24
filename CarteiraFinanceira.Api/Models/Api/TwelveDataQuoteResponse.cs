using System.Text.Json.Serialization;

namespace CarteiraFinanceira.Api.Models.Api;

public class TwelveDataQuoteResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("datetime")]
    public string DateTime { get; set; } = string.Empty;

    [JsonPropertyName("open")]
    public string Open { get; set; } = string.Empty;

    [JsonPropertyName("high")]
    public string High { get; set; } = string.Empty;

    [JsonPropertyName("low")]
    public string Low { get; set; } = string.Empty;

    [JsonPropertyName("close")]
    public string Close { get; set; } = string.Empty;

    [JsonPropertyName("previous_close")]
    public string PreviousClose { get; set; } = string.Empty;

    [JsonPropertyName("change")]
    public string Change { get; set; } = string.Empty;

    [JsonPropertyName("percent_change")]
    public string PercentChange { get; set; } = string.Empty;

    [JsonPropertyName("volume")]
    public string Volume { get; set; } = string.Empty;

    [JsonPropertyName("rolling_1d_change")]
    public string Rolling1DayChange { get; set; } = string.Empty;

    [JsonPropertyName("is_market_open")]
    public bool? IsMarketOpen { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int? Code { get; set; }
}
