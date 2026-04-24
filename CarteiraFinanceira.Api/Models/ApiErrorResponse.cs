namespace CarteiraFinanceira.Api.Models;

public class ApiErrorResponse
{
    public string Mensagem { get; set; } = string.Empty;
    public string? Detalhes { get; set; }
}
