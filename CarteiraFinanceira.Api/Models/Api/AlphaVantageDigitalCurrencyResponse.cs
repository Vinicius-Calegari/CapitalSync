using System.Text.Json.Serialization;

namespace CarteiraFinanceira.Api.Models.Api;

public class AlphaVantageDigitalCurrencyResponse
{
    [JsonPropertyName("Meta Data")]
    public AlphaVantageDigitalCurrencyMetaData? MetaData { get; set; }

    [JsonPropertyName("Time Series (Digital Currency Daily)")]
    public Dictionary<string, AlphaVantageDigitalCurrencyItem> TimeSeries { get; set; } = new();

    [JsonPropertyName("Information")]
    public string? Information { get; set; }

    [JsonPropertyName("Note")]
    public string? Note { get; set; }

    [JsonPropertyName("Error Message")]
    public string? ErrorMessage { get; set; }
}

public class AlphaVantageDigitalCurrencyMetaData
{
    [JsonPropertyName("2. Digital Currency Code")]
    public string? DigitalCurrencyCode { get; set; }

    [JsonPropertyName("3. Digital Currency Name")]
    public string? DigitalCurrencyName { get; set; }

    [JsonPropertyName("6. Last Refreshed")]
    public string? LastRefreshed { get; set; }
}

public class AlphaVantageDigitalCurrencyItem
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
