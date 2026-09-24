using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/rutas-esperadas")]
public class TareaRutaEsperadaController : ControllerBase
{
    private readonly ITareaRutaEsperadaRepository _repository;
    private readonly IGeocodingService _geocodingService;

    public TareaRutaEsperadaController(ITareaRutaEsperadaRepository repository, IGeocodingService geocodingService)
    {
        _repository = repository;
        _geocodingService = geocodingService;
    }

    [HttpGet("tarea/{tareaId}")]
    public async Task<IActionResult> ObtenerParadas(int tareaId)
    {
        var paradas = await _repository.ObtenerParadasAsync(tareaId);
        return Ok(paradas);
    }

    [HttpPut("tarea/{tareaId}")]
    public async Task<IActionResult> GuardarParadas(int tareaId, [FromBody] GuardarRutaEsperadaRequest request)
    {
        if (request.TareaId != tareaId)
            return BadRequest(new { message = "El TareaId del body no coincide con la URL." });

        try
        {
            // ── NUEVO: geocodifica cada dirección que no traiga ya coordenadas ──
            foreach (var parada in request.Paradas)
            {
                if (!string.IsNullOrWhiteSpace(parada.Direccion) && parada.Latitud is null)
                {
                    var coords = await _geocodingService.GeocodificarAsync(parada.Direccion);
                    if (coords != null)
                    {
                        parada.Latitud = coords.Value.lat;
                        parada.Longitud = coords.Value.lng;
                    }
                }
            }
            // ─────────────────────────────────────────────────────────────────

            await _repository.GuardarParadasAsync(tareaId, request.Paradas);
            var paradasGuardadas = await _repository.ObtenerParadasAsync(tareaId);
            return Ok(paradasGuardadas);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al guardar la ruta esperada.", error = ex.Message });
        }
    }
}