using Microsoft.AspNetCore.Mvc;

namespace CarteiraFinanceira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InicioController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            projeto = "Carteira Financeira Dashboard",
            descricao = "Aplicacao full stack em C# com painel web, consultas multiplas e historico de precos.",
            provedor = "Alpha Vantage + brapi",
            stack = new[]
            {
                "ASP.NET Core Web API",
                "HTML, CSS e JavaScript",
                "HttpClient",
                "System.Text.Json"
            }
        });
    }
}
