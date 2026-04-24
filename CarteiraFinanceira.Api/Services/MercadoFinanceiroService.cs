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
    private readonly string _alphaVantageApiKey = configuration["AlphaVantage:ApiKey"] ?? "GSPJPI20B0FDJ52U";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
    {
        "PETR4",
        "VALE3",
        "MGLU3",
        "ITUB4"
    };

    private static readonly Dictionary<string, string> NomesConhecidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAPL"] = "Apple Inc.",
        ["MSFT"] = "Microsoft Corporation",
        ["GOOG"] = "Alphabet Inc.",
        ["AMZN"] = "Amazon.com, Inc.",
        ["PETR4"] = "Petroleo Brasileiro S.A. - Petrobras",
        ["VALE3"] = "Vale S.A.",
        ["MGLU3"] = "Magazine Luiza S.A.",
        ["ITUB4"] = "Itau Unibanco Holding S.A.",
        ["BTC"] = "Bitcoin",
        ["ETH"] = "Ethereum"
    };

    public IReadOnlyList<SugestaoAtivoResponse> ObterSugestoesPadrao()
    {
        return SugestoesPadrao;
    }

    public async Task<AtivoFinanceiroResponse> ConsultarAtivoAsync(string ativo)
    {
        var detalhe = await ConsultarAtivoDetalhadoAsync(ativo);
        return detalhe.Ativo;
    }

    public async Task<ConsultaAtivoDetalhadaResponse> ConsultarAtivoDetalhadoAsync(string ativo, int periodoDias = 30)
    {
        var codigo = NormalizarAtivo(ativo);
        var periodoSeguro = Math.Clamp(periodoDias, 7, 100);
        var cacheKey = $"detalhe:{codigo}:{periodoSeguro}";

        if (_memoryCache.TryGetValue(cacheKey, out ConsultaAtivoDetalhadaResponse? cache) && cache is not null)
        {
            return cache;
        }

        var resultado = await ResolverConsultaAsync(codigo, periodoSeguro);
        _memoryCache.Set(cacheKey, resultado, TimeSpan.FromMinutes(5));
        _memoryCache.Set($"resumo:{codigo}", resultado.Ativo, TimeSpan.FromMinutes(5));

        return resultado;
    }

    public async Task<ConsultaLoteResponse> ConsultarMultiplosAtivosAsync(IEnumerable<string> ativos)
    {
        var lista = ativos
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizarAtivo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (lista.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um ativo valido.", nameof(ativos));
        }

        var resposta = new ConsultaLoteResponse();

        foreach (var ativo in lista)
        {
            try
            {
                resposta.Ativos.Add(await ConsultarAtivoAsync(ativo));
            }
            catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or InvalidOperationException or HttpRequestException)
            {
                resposta.Erros.Add(new ErroConsultaAtivoResponse
                {
                    Codigo = ativo,
                    Mensagem = TraduzirMensagem(ex.Message)
                });
            }
        }

        return resposta;
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ResolverConsultaAsync(string ativo, int periodoDias)
    {
        if (AtivosBrasileirosGratuitos.Contains(ativo))
        {
            return await ConsultarAcaoBrasileiraAsync(ativo, periodoDias);
        }

        if (PareceCriptomoeda(ativo))
        {
            try
            {
                return await ConsultarCriptomoedaAsync(ativo, periodoDias);
            }
            catch (KeyNotFoundException)
            {
                return await ConsultarAcaoInternacionalAsync(ativo, periodoDias);
            }
        }

        try
        {
            return await ConsultarAcaoInternacionalAsync(ativo, periodoDias);
        }
        catch (KeyNotFoundException) when (ativo.All(char.IsLetter))
        {
            return await ConsultarCriptomoedaAsync(ativo, periodoDias);
        }
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ConsultarAcaoBrasileiraAsync(string ativo, int periodoDias)
    {
        var range = MapearPeriodoBrapi(periodoDias);
        var resposta = await GetFromApiAsync<BrapiQuoteResponse>(
            $"https://brapi.dev/api/quote/{Uri.EscapeDataString(ativo)}?range={range}&interval=1d");

        if (resposta.Error || resposta.Results.Count == 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(resposta.Message)
                    ? "Nao foi possivel consultar o ativo brasileiro informado."
                    : resposta.Message);
        }

        var ativoBrapi = resposta.Results[0];
        var historico = ativoBrapi.HistoricalDataPrice
            .Select(item => new PontoHistoricoResponse
            {
                Data = ParseUnixSeconds(item.Date),
                Abertura = item.Open,
                Fechamento = item.Close,
                Maxima = item.High,
                Minima = item.Low,
                Volume = item.Volume
            })
            .Where(item => item.Fechamento > 0)
            .ToList();

        if (historico.Count == 0)
        {
            throw new InvalidOperationException("Nao foi possivel carregar o historico deste ativo brasileiro.");
        }

        return new ConsultaAtivoDetalhadaResponse
        {
            Ativo = new AtivoFinanceiroResponse
            {
                Codigo = ativoBrapi.Symbol,
                NomeAtivo = PrimeiroNaoVazio(ativoBrapi.LongName, ativoBrapi.ShortName, ResolverNomeConhecido(ativo)),
                TipoAtivo = "Acao",
                Mercado = "B3",
                PrecoAtual = ativoBrapi.RegularMarketPrice,
                VariacaoPercentual = decimal.Round(ativoBrapi.RegularMarketChangePercent, 2),
                VariacaoAbsoluta = decimal.Round(ativoBrapi.RegularMarketChange, 2),
                Moeda = PrimeiroNaoVazio(ativoBrapi.Currency, "BRL"),
                Abertura = ativoBrapi.RegularMarketOpen,
                MaximaDia = ativoBrapi.RegularMarketDayHigh,
                MinimaDia = ativoBrapi.RegularMarketDayLow,
                FechamentoAnterior = ativoBrapi.RegularMarketPreviousClose,
                Volume = ativoBrapi.RegularMarketVolume,
                StatusMercado = "Cotacao B3",
                OrigemConsulta = "brapi / Cotacao",
                UltimaAtualizacaoUtc = ParseDateOrUtcNow(ativoBrapi.RegularMarketTime)
            },
            Intervalo = "1day",
            PeriodoDias = periodoDias,
            Historico = historico
        };
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ConsultarAcaoInternacionalAsync(string ativo, int periodoDias)
    {
        foreach (var candidato in MontarCandidatosParaAcao(ativo))
        {
            List<PontoHistoricoResponse> historico;

            try
            {
                historico = await ObterHistoricoAlphaAsync(candidato, periodoDias);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            var ultimoPonto = historico[^1];
            var penultimoPonto = historico.Count > 1 ? historico[^2] : historico[^1];
            var variacaoAbsoluta = decimal.Round(ultimoPonto.Fechamento - penultimoPonto.Fechamento, 2);
            var variacaoPercentual = penultimoPonto.Fechamento == 0
                ? 0
                : decimal.Round(((ultimoPonto.Fechamento - penultimoPonto.Fechamento) / penultimoPonto.Fechamento) * 100m, 2);
            var codigoExibicao = LimparCodigo(candidato);

            return new ConsultaAtivoDetalhadaResponse
            {
                Ativo = new AtivoFinanceiroResponse
                {
                    Codigo = codigoExibicao,
                    NomeAtivo = ResolverNomeConhecido(ativo, candidato),
                    TipoAtivo = "Acao",
                    Mercado = ResolverMercadoAcao(candidato),
                    PrecoAtual = ultimoPonto.Fechamento,
                    VariacaoPercentual = variacaoPercentual,
                    VariacaoAbsoluta = variacaoAbsoluta,
                    Moeda = ResolverMoedaAcao(candidato),
                    Abertura = ultimoPonto.Abertura,
                    MaximaDia = ultimoPonto.Maxima,
                    MinimaDia = ultimoPonto.Minima,
                    FechamentoAnterior = penultimoPonto.Fechamento,
                    Volume = ultimoPonto.Volume,
                    StatusMercado = "Cotacao diaria",
                    OrigemConsulta = "Alpha Vantage / Time Series Daily",
                    UltimaAtualizacaoUtc = ultimoPonto.Data
                },
                Intervalo = "1day",
                PeriodoDias = periodoDias,
                Historico = historico
            };
        }

        throw new KeyNotFoundException(
            $"O ativo '{ativo}' nao foi encontrado. Tente codigos como AAPL, PETR4 ou BTC.");
    }

    private async Task<ConsultaAtivoDetalhadaResponse> ConsultarCriptomoedaAsync(string ativo, int periodoDias)
    {
        var resposta = await GetFromApiAsync<AlphaVantageDigitalCurrencyResponse>(
            $"https://www.alphavantage.co/query?function=DIGITAL_CURRENCY_DAILY&symbol={Uri.EscapeDataString(ativo)}&market=USD&apikey={_alphaVantageApiKey}",
            applyAlphaRateLimit: true);

        ValidarRespostaAlphaVantage(resposta.Information, resposta.Note, resposta.ErrorMessage);

        if (resposta.MetaData is null || resposta.TimeSeries.Count == 0)
        {
            throw new KeyNotFoundException(
                $"A criptomoeda '{ativo}' nao foi encontrada. Tente codigos como BTC ou ETH.");
        }

        var historico = resposta.TimeSeries
            .OrderByDescending(item => item.Key, StringComparer.Ordinal)
            .Take(Math.Max(periodoDias, 2))
            .Select(item => new PontoHistoricoResponse
            {
                Data = ParseDateOrUtcNow(item.Key),
                Abertura = ParseDecimal(item.Value.Open),
                Fechamento = ParseDecimal(item.Value.Close),
                Maxima = ParseDecimal(item.Value.High),
                Minima = ParseDecimal(item.Value.Low),
                Volume = ParseLongFlexivel(item.Value.Volume)
            })
            .Where(item => item.Fechamento > 0)
            .OrderBy(item => item.Data)
            .ToList();

        if (historico.Count == 0)
        {
            throw new InvalidOperationException("Nao foi possivel carregar o historico da criptomoeda.");
        }

        var atual = historico[^1];
        var anterior = historico.Count > 1 ? historico[^2] : historico[^1];
        var variacaoAbsoluta = decimal.Round(atual.Fechamento - anterior.Fechamento, 2);
        var variacaoPercentual = anterior.Fechamento == 0
            ? 0
            : decimal.Round(((atual.Fechamento - anterior.Fechamento) / anterior.Fechamento) * 100m, 2);

        return new ConsultaAtivoDetalhadaResponse
        {
            Ativo = new AtivoFinanceiroResponse
            {
                Codigo = PrimeiroNaoVazio(resposta.MetaData.DigitalCurrencyCode, ativo),
                NomeAtivo = PrimeiroNaoVazio(resposta.MetaData.DigitalCurrencyName, ResolverNomeConhecido(ativo)),
                TipoAtivo = "Criptomoeda",
                Mercado = "Mercado cripto",
                PrecoAtual = atual.Fechamento,
                VariacaoPercentual = variacaoPercentual,
                VariacaoAbsoluta = variacaoAbsoluta,
                Moeda = "USD",
                Abertura = atual.Abertura,
                MaximaDia = atual.Maxima,
                MinimaDia = atual.Minima,
                FechamentoAnterior = anterior.Fechamento,
                Volume = atual.Volume,
                StatusMercado = "Mercado cripto 24/7",
                OrigemConsulta = "Alpha Vantage / Digital Currency Daily",
                UltimaAtualizacaoUtc = ParseDateOrUtcNow(resposta.MetaData.LastRefreshed)
            },
            Intervalo = "1day",
            PeriodoDias = periodoDias,
            Historico = historico
        };
    }

    private async Task<List<PontoHistoricoResponse>> ObterHistoricoAlphaAsync(string simbolo, int periodoDias)
    {
        var resposta = await GetFromApiAsync<AlphaVantageDailyResponse>(
            $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(simbolo)}&outputsize=compact&apikey={_alphaVantageApiKey}",
            applyAlphaRateLimit: true);

        ValidarRespostaAlphaVantage(resposta.Information, resposta.Note, resposta.ErrorMessage);

        if (resposta.TimeSeries.Count == 0)
        {
            throw new KeyNotFoundException($"O ativo '{LimparCodigo(simbolo)}' nao foi encontrado.");
        }

        var historico = resposta.TimeSeries
            .OrderByDescending(item => item.Key, StringComparer.Ordinal)
            .Take(periodoDias)
            .Select(item => new PontoHistoricoResponse
            {
                Data = ParseDateOrUtcNow(item.Key),
                Abertura = ParseDecimal(item.Value.Open),
                Fechamento = ParseDecimal(item.Value.Close),
                Maxima = ParseDecimal(item.Value.High),
                Minima = ParseDecimal(item.Value.Low),
                Volume = ParseLong(item.Value.Volume)
            })
            .Where(item => item.Fechamento > 0)
            .OrderBy(item => item.Data)
            .ToList();

        return historico;
    }

    private async Task<T> GetFromApiAsync<T>(string url, bool applyAlphaRateLimit = false)
    {
        if (applyAlphaRateLimit)
        {
            await RespeitarRateLimitAlphaAsync();
        }

        using var response = await _httpClient.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Falha ao consultar a API externa. Status HTTP: {(int)response.StatusCode}.");
        }

        var resultado = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return resultado ?? throw new InvalidOperationException("Nao foi possivel interpretar a resposta da API.");
    }

    private static void ValidarRespostaAlphaVantage(string? information, string? note, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            throw new InvalidOperationException(
                "A Alpha Vantage atingiu o limite temporario da chave configurada. Aguarde alguns segundos e tente novamente.");
        }

        if (!string.IsNullOrWhiteSpace(information))
        {
            throw new InvalidOperationException(information);
        }
    }

    private static string NormalizarAtivo(string ativo)
    {
        if (string.IsNullOrWhiteSpace(ativo))
        {
            throw new ArgumentException("Informe um codigo de ativo valido.", nameof(ativo));
        }

        return ativo.Trim().ToUpperInvariant();
    }

    private static bool PareceCriptomoeda(string ativo)
    {
        return ativo.Length is >= 2 and <= 6 &&
               ativo.All(char.IsLetter) &&
               !AtivosBrasileirosGratuitos.Contains(ativo) &&
               !ativo.Equals("AAPL", StringComparison.OrdinalIgnoreCase) &&
               !ativo.Equals("MSFT", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> MontarCandidatosParaAcao(string ativo)
    {
        var candidatos = new List<string> { ativo };

        if (PareceAcaoBrasileira(ativo))
        {
            candidatos.Add($"{ativo}.SAO");
            candidatos.Add($"{ativo}.BVMF");
        }

        return candidatos
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool PareceAcaoBrasileira(string ativo)
    {
        return ativo.Length >= 5 &&
               ativo.Take(4).All(char.IsLetter) &&
               ativo.Skip(4).All(char.IsDigit);
    }

    private static string ResolverNomeConhecido(params string?[] codigos)
    {
        foreach (var codigo in codigos)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                continue;
            }

            var limpo = LimparCodigo(codigo);

            if (NomesConhecidos.TryGetValue(limpo, out var nome))
            {
                return nome;
            }
        }

        return LimparCodigo(codigos.FirstOrDefault() ?? string.Empty);
    }

    private static string LimparCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return string.Empty;
        }

        var separador = codigo.IndexOf('.');
        return separador > 0 ? codigo[..separador] : codigo;
    }

    private static string ResolverMercadoAcao(string simbolo)
    {
        if (simbolo.EndsWith(".SAO", StringComparison.OrdinalIgnoreCase) ||
            simbolo.EndsWith(".BVMF", StringComparison.OrdinalIgnoreCase))
        {
            return "B3";
        }

        return "Mercado internacional";
    }

    private static string ResolverMoedaAcao(string simbolo)
    {
        return ResolverMercadoAcao(simbolo) == "B3" ? "BRL" : "USD";
    }

    private static string PrimeiroNaoVazio(params string?[] valores)
    {
        foreach (var valor in valores)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return string.Empty;
    }

    private static decimal SelecionarDecimal(decimal principal, decimal fallback)
    {
        return principal != 0 ? principal : fallback;
    }

    private static long SelecionarLong(long principal, long fallback)
    {
        return principal != 0 ? principal : fallback;
    }

    private static decimal ParseDecimal(string? valor)
    {
        if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var convertido))
        {
            return decimal.Round(convertido, 2);
        }

        return 0m;
    }

    private static long ParseLong(string? valor)
    {
        return long.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var convertido)
            ? convertido
            : 0L;
    }

    private static long ParseLongFlexivel(string? valor)
    {
        if (long.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var inteiro))
        {
            return inteiro;
        }

        if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValor))
        {
            return (long)Math.Round(decimalValor, MidpointRounding.AwayFromZero);
        }

        return 0L;
    }

    private static DateTime ParseDateOrUtcNow(string? valor)
    {
        if (DateTime.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var convertido))
        {
            return convertido;
        }

        return DateTime.UtcNow;
    }

    private static DateTime ParseUnixSeconds(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
    }

    private static string MapearPeriodoBrapi(int periodoDias)
    {
        return periodoDias switch
        {
            <= 7 => "5d",
            <= 30 => "1mo",
            <= 90 => "3mo",
            _ => "6mo"
        };
    }

    private static string TraduzirMensagem(string mensagem)
    {
        if (mensagem.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("credits", StringComparison.OrdinalIgnoreCase))
        {
            return "A API externa atingiu o limite temporario. Aguarde alguns instantes e tente novamente.";
        }

        if (mensagem.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("nao foi encontrado", StringComparison.OrdinalIgnoreCase))
        {
            return "Ativo nao encontrado. Verifique o codigo digitado.";
        }

        return mensagem;
    }

    private static async Task RespeitarRateLimitAlphaAsync()
    {
        await AlphaRateLimitLock.WaitAsync();

        try
        {
            var intervalo = DateTimeOffset.UtcNow - _lastAlphaRequestAt;

            if (intervalo.TotalMilliseconds < AlphaRequestIntervalMilliseconds)
            {
                await Task.Delay(AlphaRequestIntervalMilliseconds - (int)intervalo.TotalMilliseconds);
            }

            _lastAlphaRequestAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            AlphaRateLimitLock.Release();
        }
    }
}
