namespace CarteiraFinanceira.Api.Models;

public class AtivoFinanceiroResponse
{
    public string Codigo { get; set; } = string.Empty;
    public string NomeAtivo { get; set; } = string.Empty;
    public string TipoAtivo { get; set; } = string.Empty;
    public string Mercado { get; set; } = string.Empty;
    public decimal PrecoAtual { get; set; }
    public decimal VariacaoPercentual { get; set; }
    public decimal VariacaoAbsoluta { get; set; }
    public string Moeda { get; set; } = string.Empty;
    public decimal Abertura { get; set; }
    public decimal MaximaDia { get; set; }
    public decimal MinimaDia { get; set; }
    public decimal FechamentoAnterior { get; set; }
    public long Volume { get; set; }
    public string StatusMercado { get; set; } = string.Empty;
    public string OrigemConsulta { get; set; } = string.Empty;
    public DateTime UltimaAtualizacaoUtc { get; set; }
}
