using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/rutas")]
public class TareaRutaController : ControllerBase
{
    private readonly ITareaRutaService _service;
    private readonly ITareaRutaRepository _repository;

    public TareaRutaController(ITareaRutaService service, ITareaRutaRepository repository)
    {
        _service = service;
        _repository = repository;
    }

    [HttpGet("tarea/{tareaId}/activa")]
    public async Task<IActionResult> ObtenerRutaActiva(int tareaId)
    {
        var ruta = await _repository.ObtenerRutaActivaAsync(tareaId);
        return ruta == null ? NotFound() : Ok(ruta);
    }

    [HttpPost("iniciar")]
    public async Task<IActionResult> IniciarRuta([FromBody] IniciarRutaRequest request)
    {
        try
        {
            var ruta = await _service.IniciarRutaAsync(request);
            return Ok(ruta);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{rutaId}/finalizar")]
    public async Task<IActionResult> FinalizarRuta(int rutaId, [FromBody] FinalizarRutaRequest request)
    {
        try
        {
            var ruta = await _service.FinalizarRutaAsync(rutaId, request);
            return Ok(ruta);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{rutaId}/resumen")]
    public async Task<IActionResult> ObtenerResumen(int rutaId)
    {
        try
        {
            var resumen = await _service.ObtenerResumenAsync(rutaId);
            return Ok(resumen);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    [HttpGet("tarea/{tareaId}/avance")]
    public async Task<IActionResult> ObtenerAvanceConRuta(int tareaId)
    {
        var ultimaRuta = await _repository.ObtenerUltimaRutaPorTareaAsync(tareaId);
        if (ultimaRuta == null)
            return NotFound();

        try
        {
            var resumen = await _service.ObtenerResumenAsync(ultimaRuta.Id);
            return Ok(resumen);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}