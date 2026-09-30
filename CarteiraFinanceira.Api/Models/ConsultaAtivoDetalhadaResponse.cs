namespace CarteiraFinanceira.Api.Models;

public class ConsultaAtivoDetalhadaResponse
{
    public AtivoFinanceiroResponse Ativo { get; set; } = new();
    public string Intervalo { get; set; } = "1day";
    public int PeriodoDias { get; set; }
    public List<PontoHistoricoResponse> Historico { get; set; } = [];
}

public class PontoHistoricoResponse
{
    public DateTimeOffset Data { get; set; }
    public decimal Abertura { get; set; }
    public decimal Fechamento { get; set; }
    public decimal Maxima { get; set; }
    public decimal Minima { get; set; }
    public long Volume { get; set; }
}
