namespace CarteiraFinanceira.Api.Models;

public class ConsultaLoteResponse
{
    public List<AtivoFinanceiroResponse> Ativos { get; set; } = [];
    public List<ErroConsultaAtivoResponse> Erros { get; set; } = [];
}

public class ErroConsultaAtivoResponse
{
    public string Codigo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
}
