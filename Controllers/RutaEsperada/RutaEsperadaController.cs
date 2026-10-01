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
            // Trae lo ya guardado, para reutilizar coordenadas si el texto no cambió
            var paradasExistentes = await _repository.ObtenerParadasAsync(tareaId);

            // Geocodifica en PARALELO solo las direcciones nuevas o modificadas
            var tareasGeocoding = request.Paradas.Select(async parada =>
            {
                var existente = paradasExistentes.FirstOrDefault(p =>
                    p.Orden == parada.Orden &&
                    p.TipoParada == parada.TipoParada &&
                    string.Equals(p.Direccion?.Trim(), parada.Direccion?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (existente?.Latitud != null && existente.Longitud != null)
                {
                    // Dirección sin cambios — reutiliza coordenadas, sin llamar a Google
                    parada.Latitud = existente.Latitud;
                    parada.Longitud = existente.Longitud;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(parada.Direccion))
                {
                    var coords = await _geocodingService.GeocodificarAsync(parada.Direccion);
                    if (coords != null)
                    {
                        parada.Latitud = coords.Value.lat;
                        parada.Longitud = coords.Value.lng;
                    }
                }
            });

            await Task.WhenAll(tareasGeocoding); // ← en paralelo, no secuencial

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