using CarteiraFinanceira.Api.Models;
using CarteiraFinanceira.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarteiraFinanceira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AtivosController(IConsultaAtivoService consultaAtivoService) : ControllerBase
{
    private readonly IConsultaAtivoService _consultaAtivoService = consultaAtivoService;

    [HttpGet("consultar")]
    [ProducesResponseType(typeof(ConsultaAtivoDetalhadaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Consultar([FromQuery] string ativo, [FromQuery] int periodo = 30)
    {
        if (string.IsNullOrWhiteSpace(ativo))
        {
            return BadRequest(new ApiErrorResponse
            {
                Mensagem = "Informe o c\u00f3digo do ativo para consulta."
            });
        }

        try
        {
            var result = await _consultaAtivoService.ConsultarAtivoDetalhadoAsync(ativo, periodo);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse { Mensagem = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse { Mensagem = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ApiErrorResponse
            {
                Mensagem = "N\u00e3o foi poss\u00edvel obter dados da API externa.",
                Detalhes = ex.Message
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiErrorResponse
            {
                Mensagem = "N\u00e3o foi poss\u00edvel concluir a consulta.",
                Detalhes = ex.Message
            });
        }
    }

    [HttpGet("multiplas")]
    [ProducesResponseType(typeof(ConsultaLoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarMultiplas([FromQuery] string ativos)
    {
        if (string.IsNullOrWhiteSpace(ativos))
        {
            return BadRequest(new ApiErrorResponse
            {
                Mensagem = "Informe um ou mais ativos separados por v\u00edrgula."
            });
        }

        try
        {
            var assetList = ativos
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var result = await _consultaAtivoService.ConsultarMultiplosAtivosAsync(assetList);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiErrorResponse
            {
                Mensagem = "N\u00e3o foi poss\u00edvel concluir a consulta em lote.",
                Detalhes = ex.Message
            });
        }
    }

    [HttpGet("sugestoes")]
    [ProducesResponseType(typeof(IReadOnlyList<SugestaoAtivoResponse>), StatusCodes.Status200OK)]
    public IActionResult Sugestoes()
    {
        return Ok(_consultaAtivoService.ObterSugestoesPadrao());
    }
}
