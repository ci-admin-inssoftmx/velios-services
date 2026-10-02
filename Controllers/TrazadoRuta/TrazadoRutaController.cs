using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Trazado de una ruta por calles y carreteras (modo 2 del mapa en la web proveedor).
/// Controlador nuevo e independiente: no modifica TareaRutaController.
/// [Authorize] porque cada cálculo nuevo puede costar una consulta a Google.
/// </summary>
[ApiController]
[Authorize]
[Route("api/rutas/{rutaId:int}/trazado")]
public class TrazadoRutaController : ControllerBase
{
    private readonly ITrazadoRutaService _service;

    public TrazadoRutaController(ITrazadoRutaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int rutaId, CancellationToken ct)
    {
        try
        {
            var trazado = await _service.ObtenerTrazadoAsync(rutaId, ct);
            return Ok(trazado);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}