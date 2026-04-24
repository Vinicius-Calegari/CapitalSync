using System.Text.Json.Serialization;

namespace CarteiraFinanceira.Api.Models.Api;

public class AlphaVantageDailyResponse
{
    [JsonPropertyName("Meta Data")]
    public AlphaVantageDailyMetaData? MetaData { get; set; }

    [JsonPropertyName("Time Series (Daily)")]
    public Dictionary<string, AlphaVantageDailyItem> TimeSeries { get; set; } = new();

    [JsonPropertyName("Information")]
    public string? Information { get; set; }

    [JsonPropertyName("Note")]
    public string? Note { get; set; }

    [JsonPropertyName("Error Message")]
    public string? ErrorMessage { get; set; }
}

public class AlphaVantageDailyMetaData
{
    [JsonPropertyName("2. Symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("3. Last Refreshed")]
    public string? LastRefreshed { get; set; }
}

public class AlphaVantageDailyItem
{
    [JsonPropertyName("1. open")]
    public string? Open { get; set; }

    [JsonPropertyName("2. high")]
    public string? High { get; set; }

    [JsonPropertyName("3. low")]
    public string? Low { get; set; }

    [JsonPropertyName("4. close")]
    public string? Close { get; set; }

    [JsonPropertyName("5. volume")]
    public string? Volume { get; set; }
}
