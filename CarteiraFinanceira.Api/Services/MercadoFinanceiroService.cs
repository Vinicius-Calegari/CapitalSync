using System.Globalization;
using System.Text.Json;
using CarteiraFinanceira.Api.Models;
using CarteiraFinanceira.Api.Models.Api;
using Microsoft.Extensions.Caching.Memory;

namespace CarteiraFinanceira.Api.Services;

public class MercadoFinanceiroService(HttpClient httpClient, IConfiguration configuration, IMemoryCache memoryCache)
    : IConsultaAtivoService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly string _alphaVantageApiKey = configuration["AlphaVantage:ApiKey"]
        ?? throw new InvalidOperationException("Configure AlphaVantage:ApiKey via variável de ambiente ou User Secrets.");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim AlphaRateLimitLock = new(1, 1);
    private static DateTimeOffset _lastAlphaRequestAt = DateTimeOffset.MinValue;
    private const int AlphaRequestIntervalMilliseconds = 1250;

    private static readonly IReadOnlyList<SugestaoAtivoResponse> SugestoesPadrao =
    [
        new() { Codigo = "AAPL", Nome = "Apple", Categoria = "Acoes internacionais" },
        new() { Codigo = "MSFT", Nome = "Microsoft", Categoria = "Tecnologia" },
        new() { Codigo = "PETR4", Nome = "Petrobras", Categoria = "Bolsa brasileira" },
        new() { Codigo = "VALE3", Nome = "Vale", Categoria = "Bolsa brasileira" },
        new() { Codigo = "BTC", Nome = "Bitcoin", Categoria = "Criptomoedas" },
        new() { Codigo = "ETH", Nome = "Ethereum", Categoria = "Criptomoedas" }
    ];

    private static readonly HashSet<string> AtivosBrasileirosGratuitos = new(StringComparer.OrdinalIgnoreCase)
    { "PETR4", "VALE3", "MGLU3", "ITUB4" };

    private static readonly Dictionary<string, string> NomesConhecidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAPL"] = "Apple Inc.", ["MSFT"] = "Microsoft Corporation", ["GOOG"] = "Alphabet Inc.",
        ["AMZN"] = "Amazon.com, Inc.", ["PETR4"] = "Petroleo Brasileiro S.A. - Petrobras",
        ["VALE3"] = "Vale S.A.", ["MGLU3"] = "Magazine Luiza S.A.", ["ITUB4"] = "Itau Unibanco Holding S.A.",
        ["BTC"] = "Bitcoin", ["ETH"] = "Ethereum"
    };

    public IReadOnlyList<SugestaoAtivoResponse> ObterSugestoesPadrao() => SugestoesPadrao;
    public async Task<AtivoFinanceiroResponse> ConsultarAtivoAsync(string ativo) => (await ConsultarAtivoDetalhadoAsync(ativo)).Ativo;

    public async Task<ConsultaAtivoDetalhadaResponse> ConsultarAtivoDetalhadoAsync(string ativo, int periodoDias = 30)
    {
        var codigo = NormalizarAtivo(ativo);
        var periodoSeguro = Math.Clamp(periodoDias, 7, 100);
        var cacheKey = $"detalhe:{codigo}:{periodoSeguro}";
        if (_memoryCache.TryGetValue(cacheKey, out ConsultaAtivoDetalhadaResponse? cache) && cache is not null) return cache;
        var resultado = await ResolverConsultaAsync(codigo, periodoSeguro);
        _memoryCache.Set(cacheKey, resultado, TimeSpan.FromMinutes(5));
        _memoryCache.Set($"resumo:{codigo}", resultado.Ativo, TimeSpan.FromMinutes(5));
        return resultado;
    }

    public async Task<ConsultaLoteResponse> ConsultarMultiplosAtivosAsync(IEnumerable<string> ativos)
    {
        var lista = ativos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizarAtivo).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        if (lista.Count == 0) throw new ArgumentException("Informe ao menos um ativo valido.", nameof(ativos));
        var resposta = new ConsultaLoteResponse();
        foreach (var ativo in lista)
        {
            try { resposta.Ativos.Add(await ConsultarAtivoAsync(ativo)); }
            catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or InvalidOperationException or HttpRequestException)
            { resposta.Erros.Add(new ErroConsultaAtivoResponse { Codigo = ativo, Mensagem = TraduzirMensagem(ex.Message) }); }
        }
        return resposta;
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ResolverConsultaAsync(string ativo, int periodoDias)
    {
        if (AtivosBrasileirosGratuitos.Contains(ativo)) return await ConsultarAcaoBrasileiraAsync(ativo, periodoDias);
        if (PareceCriptomoeda(ativo)) { try { return await ConsultarCriptomoedaAsync(ativo, periodoDias); } catch (KeyNotFoundException) { return await ConsultarAcaoInternacionalAsync(ativo, periodoDias); } }
        try { return await ConsultarAcaoInternacionalAsync(ativo, periodoDias); }
        catch (KeyNotFoundException) when (ativo.All(char.IsLetter)) { return await ConsultarCriptomoedaAsync(ativo, periodoDias); }
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ConsultarAcaoBrasileiraAsync(string ativo, int periodoDias)
    {
        var resposta = await GetFromApiAsync<BrapiQuoteResponse>($"https://brapi.dev/api/quote/{Uri.EscapeDataString(ativo)}?range={MapearPeriodoBrapi(periodoDias)}&interval=1d");
        if (resposta.Error || resposta.Results.Count == 0) throw new InvalidOperationException("Nao foi possivel consultar o ativo brasileiro informado.");
        var a = resposta.Results[0];
        var h = a.HistoricalDataPrice.Select(x => new PontoHistoricoResponse { Data = ParseUnixSeconds(x.Date), Abertura = x.Open, Fechamento = x.Close, Maxima = x.High, Minima = x.Low, Volume = x.Volume }).Where(x => x.Fechamento > 0).ToList();
        if (h.Count == 0) throw new InvalidOperationException("Nao foi possivel carregar o historico deste ativo brasileiro.");
        return new ConsultaAtivoDetalhadaResponse { Ativo = new AtivoFinanceiroResponse { Codigo = a.Symbol, NomeAtivo = PrimeiroNaoVazio(a.LongName, a.ShortName, ResolverNomeConhecido(ativo)), TipoAtivo = "Acao", Mercado = "B3", PrecoAtual = a.RegularMarketPrice, VariacaoPercentual = decimal.Round(a.RegularMarketChangePercent, 2), VariacaoAbsoluta = decimal.Round(a.RegularMarketChange, 2), Moeda = PrimeiroNaoVazio(a.Currency, "BRL"), Abertura = a.RegularMarketOpen, MaximaDia = a.RegularMarketDayHigh, MinimaDia = a.RegularMarketDayLow, FechamentoAnterior = a.RegularMarketPreviousClose, Volume = a.RegularMarketVolume, StatusMercado = "Cotacao B3", OrigemConsulta = "brapi / Cotacao", UltimaAtualizacaoUtc = ParseDateOrUtcNow(a.RegularMarketTime) }, Intervalo = "1day", PeriodoDias = periodoDias, Historico = h };
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ConsultarAcaoInternacionalAsync(string ativo, int periodoDias)
    {
        foreach (var candidato in MontarCandidatosParaAcao(ativo))
        {
            List<PontoHistoricoResponse> h; try { h = await ObterHistoricoAlphaAsync(candidato, periodoDias); } catch (KeyNotFoundException) { continue; }
            var atual = h[^1]; var anterior = h.Count > 1 ? h[^2] : h[^1]; var abs = decimal.Round(atual.Fechamento - anterior.Fechamento, 2); var pct = anterior.Fechamento == 0 ? 0 : decimal.Round(abs / anterior.Fechamento * 100m, 2);
            return new ConsultaAtivoDetalhadaResponse { Ativo = new AtivoFinanceiroResponse { Codigo = LimparCodigo(candidato), NomeAtivo = ResolverNomeConhecido(ativo, candidato), TipoAtivo = "Acao", Mercado = ResolverMercadoAcao(candidato), PrecoAtual = atual.Fechamento, VariacaoPercentual = pct, VariacaoAbsoluta = abs, Moeda = ResolverMoedaAcao(candidato), Abertura = atual.Abertura, MaximaDia = atual.Maxima, MinimaDia = atual.Minima, FechamentoAnterior = anterior.Fechamento, Volume = atual.Volume, StatusMercado = "Cotacao diaria", OrigemConsulta = "Alpha Vantage / Time Series Daily", UltimaAtualizacaoUtc = atual.Data }, Intervalo = "1day", PeriodoDias = periodoDias, Historico = h };
        }
        throw new KeyNotFoundException($"O ativo '{ativo}' nao foi encontrado. Tente codigos como AAPL, PETR4 ou BTC.");
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ConsultarCriptomoedaAsync(string ativo, int periodoDias)
    {
        var r = await GetFromApiAsync<AlphaVantageDigitalCurrencyResponse>($"https://www.alphavantage.co/query?function=DIGITAL_CURRENCY_DAILY&symbol={Uri.EscapeDataString(ativo)}&market=USD&apikey={Uri.EscapeDataString(_alphaVantageApiKey)}", true);
        ValidarRespostaAlphaVantage(r.Information, r.Note, r.ErrorMessage);
        if (r.MetaData is null || r.TimeSeries.Count == 0) throw new KeyNotFoundException($"A criptomoeda '{ativo}' nao foi encontrada.");
        var h = r.TimeSeries.OrderByDescending(x => x.Key, StringComparer.Ordinal).Take(Math.Max(periodoDias, 2)).Select(x => new PontoHistoricoResponse { Data = ParseDateOrUtcNow(x.Key), Abertura = ParseDecimal(x.Value.Open), Fechamento = ParseDecimal(x.Value.Close), Maxima = ParseDecimal(x.Value.High), Minima = ParseDecimal(x.Value.Low), Volume = ParseLongFlexivel(x.Value.Volume) }).Where(x => x.Fechamento > 0).OrderBy(x => x.Data).ToList();
        if (h.Count == 0) throw new InvalidOperationException("Nao foi possivel carregar o historico da criptomoeda.");
        var atual = h[^1]; var anterior = h.Count > 1 ? h[^2] : h[^1]; var abs = decimal.Round(atual.Fechamento - anterior.Fechamento, 2); var pct = anterior.Fechamento == 0 ? 0 : decimal.Round(abs / anterior.Fechamento * 100m, 2);
        return new ConsultaAtivoDetalhadaResponse { Ativo = new AtivoFinanceiroResponse { Codigo = PrimeiroNaoVazio(r.MetaData.DigitalCurrencyCode, ativo), NomeAtivo = PrimeiroNaoVazio(r.MetaData.DigitalCurrencyName, ResolverNomeConhecido(ativo)), TipoAtivo = "Criptomoeda", Mercado = "Mercado cripto", PrecoAtual = atual.Fechamento, VariacaoPercentual = pct, VariacaoAbsoluta = abs, Moeda = "USD", Abertura = atual.Abertura, MaximaDia = atual.Maxima, MinimaDia = atual.Minima, FechamentoAnterior = anterior.Fechamento, Volume = atual.Volume, StatusMercado = "Mercado cripto 24/7", OrigemConsulta = "Alpha Vantage / Digital Currency Daily", UltimaAtualizacaoUtc = ParseDateOrUtcNow(r.MetaData.LastRefreshed) }, Intervalo = "1day", PeriodoDias = periodoDias, Historico = h };
    }

    private async Task<List<PontoHistoricoResponse>> ObterHistoricoAlphaAsync(string simbolo, int periodoDias)
    {
        var r = await GetFromApiAsync<AlphaVantageDailyResponse>($"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(simbolo)}&outputsize=compact&apikey={Uri.EscapeDataString(_alphaVantageApiKey)}", true);
        ValidarRespostaAlphaVantage(r.Information, r.Note, r.ErrorMessage);
        if (r.TimeSeries.Count == 0) throw new KeyNotFoundException($"O ativo '{LimparCodigo(simbolo)}' nao foi encontrado.");
        return r.TimeSeries.OrderByDescending(x => x.Key, StringComparer.Ordinal).Take(periodoDias).Select(x => new PontoHistoricoResponse { Data = ParseDateOrUtcNow(x.Key), Abertura = ParseDecimal(x.Value.Open), Fechamento = ParseDecimal(x.Value.Close), Maxima = ParseDecimal(x.Value.High), Minima = ParseDecimal(x.Value.Low), Volume = ParseLong(x.Value.Volume) }).Where(x => x.Fechamento > 0).OrderBy(x => x.Data).ToList();
    }

    private async Task<T> GetFromApiAsync<T>(string url, bool applyAlphaRateLimit = false)
    {
        if (applyAlphaRateLimit) await RespeitarRateLimitAlphaAsync();
        using var response = await _httpClient.GetAsync(url); var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Falha ao consultar a API externa. Status HTTP: {(int)response.StatusCode}.");
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("Nao foi possivel interpretar a resposta da API.");
    }

    private static async Task RespeitarRateLimitAlphaAsync() { await AlphaRateLimitLock.WaitAsync(); try { var espera = TimeSpan.FromMilliseconds(AlphaRequestIntervalMilliseconds) - (DateTimeOffset.UtcNow - _lastAlphaRequestAt); if (espera > TimeSpan.Zero) await Task.Delay(espera); _lastAlphaRequestAt = DateTimeOffset.UtcNow; } finally { AlphaRateLimitLock.Release(); } }
    private static string NormalizarAtivo(string ativo) { var valor = ativo?.Trim().ToUpperInvariant() ?? string.Empty; if (valor.Length is < 1 or > 15 || valor.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '-')) throw new ArgumentException("Codigo de ativo invalido.", nameof(ativo)); return valor; }
    private static bool PareceCriptomoeda(string ativo) => ativo is "BTC" or "ETH" or "SOL" or "ADA" or "XRP" or "DOGE";
    private static IEnumerable<string> MontarCandidatosParaAcao(string ativo) { yield return ativo; if (ativo.EndsWith("3") || ativo.EndsWith("4")) yield return $"{ativo}.SA"; }
    private static string LimparCodigo(string codigo) => codigo.EndsWith(".SA", StringComparison.OrdinalIgnoreCase) ? codigo[..^3] : codigo;
    private static string ResolverMercadoAcao(string codigo) => codigo.EndsWith(".SA", StringComparison.OrdinalIgnoreCase) ? "B3" : "Mercado internacional";
    private static string ResolverMoedaAcao(string codigo) => codigo.EndsWith(".SA", StringComparison.OrdinalIgnoreCase) ? "BRL" : "USD";
    private static string ResolverNomeConhecido(params string[] codigos) { foreach (var c in codigos) if (NomesConhecidos.TryGetValue(LimparCodigo(c), out var nome)) return nome; return LimparCodigo(codigos[0]); }
    private static string PrimeiroNaoVazio(params string?[] valores) => valores.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static DateTimeOffset ParseUnixSeconds(long value) => DateTimeOffset.FromUnixTimeSeconds(value);
    private static DateTimeOffset ParseDateOrUtcNow(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : DateTimeOffset.UtcNow;
    private static decimal ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static long ParseLong(string? value) => long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static long ParseLongFlexivel(string? value) => (long)ParseDecimal(value);
    private static string MapearPeriodoBrapi(int dias) => dias <= 7 ? "7d" : dias <= 30 ? "1mo" : dias <= 90 ? "3mo" : "6mo";
    private static void ValidarRespostaAlphaVantage(string? information, string? note, string? error) { var msg = PrimeiroNaoVazio(error, note, information); if (!string.IsNullOrWhiteSpace(msg)) throw new InvalidOperationException(msg); }
    private static string TraduzirMensagem(string mensagem) => mensagem;
}
