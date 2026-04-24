using System.Text.Json.Serialization;

namespace CarteiraFinanceira.Api.Models.Api;

public class TwelveDataTimeSeriesResponse
{
    [JsonPropertyName("meta")]
    public TwelveDataTimeSeriesMeta? Meta { get; set; }

    [JsonPropertyName("values")]
    public List<TwelveDataTimeSeriesValue> Values { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int? Code { get; set; }
}

public class TwelveDataTimeSeriesMeta
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("interval")]
    public string Interval { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = string.Empty;

    [JsonPropertyName("exchange_timezone")]
    public string ExchangeTimezone { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class TwelveDataTimeSeriesValue
{
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

    [JsonPropertyName("volume")]
    public string Volume { get; set; } = string.Empty;
}
