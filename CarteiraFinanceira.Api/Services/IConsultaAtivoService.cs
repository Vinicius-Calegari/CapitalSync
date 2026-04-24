using CarteiraFinanceira.Api.Models;

namespace CarteiraFinanceira.Api.Services;

public interface IConsultaAtivoService
{
    Task<AtivoFinanceiroResponse> ConsultarAtivoAsync(string ativo);
    Task<ConsultaAtivoDetalhadaResponse> ConsultarAtivoDetalhadoAsync(string ativo, int periodoDias = 30);
    Task<ConsultaLoteResponse> ConsultarMultiplosAtivosAsync(IEnumerable<string> ativos);
    IReadOnlyList<SugestaoAtivoResponse> ObterSugestoesPadrao();
}
