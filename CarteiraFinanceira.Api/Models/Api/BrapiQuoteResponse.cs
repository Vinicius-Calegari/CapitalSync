using System.Text.Json.Serialization;

namespace CarteiraFinanceira.Api.Models.Api;

public class BrapiQuoteResponse
{
    [JsonPropertyName("results")]
    public List<BrapiQuoteItem> Results { get; set; } = [];

    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class BrapiQuoteItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    [JsonPropertyName("longName")]
    public string? LongName { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("regularMarketChange")]
    public decimal RegularMarketChange { get; set; }

    [JsonPropertyName("regularMarketChangePercent")]
    public decimal RegularMarketChangePercent { get; set; }

    [JsonPropertyName("regularMarketTime")]
    public string? RegularMarketTime { get; set; }

    [JsonPropertyName("regularMarketPrice")]
    public decimal RegularMarketPrice { get; set; }

    [JsonPropertyName("regularMarketDayHigh")]
    public decimal RegularMarketDayHigh { get; set; }

    [JsonPropertyName("regularMarketDayLow")]
    public decimal RegularMarketDayLow { get; set; }

    [JsonPropertyName("regularMarketVolume")]
    public long RegularMarketVolume { get; set; }

    [JsonPropertyName("regularMarketPreviousClose")]
    public decimal RegularMarketPreviousClose { get; set; }

    [JsonPropertyName("regularMarketOpen")]
    public decimal RegularMarketOpen { get; set; }

    [JsonPropertyName("historicalDataPrice")]
    public List<BrapiHistoricalPriceItem> HistoricalDataPrice { get; set; } = [];
}

public class BrapiHistoricalPriceItem
{
    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("open")]
    public decimal Open { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }

    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("close")]
    public decimal Close { get; set; }

    [JsonPropertyName("volume")]
    public long Volume { get; set; }
}
