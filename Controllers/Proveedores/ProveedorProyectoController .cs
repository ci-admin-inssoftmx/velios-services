using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Proveedores;

[ApiController]
[Route("api/ProveedorProyecto")]
public class ProveedorProyectoController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProveedorProyectoController(AppDbContext db) => _db = db;

    // =========================
    // Asignar proyecto
    // =========================
    [HttpPost("Asignar")]
    public async Task<IActionResult> Asignar(ProveedorProyectoCreateRequest model)
    {
        var entity = new ProveedorProyecto
        {
            ProveedorId = model.ProveedorId,
            ProyectoNombre = model.ProyectoNombre,
            ClienteNombre = model.ClienteNombre,
            FechaAsignacion = DateTime.UtcNow,
            EstatusProyectoProveedorId = 1
        };

        _db.ProveedorProyectos.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(entity);
    }

    // =========================
    // Aceptar / Rechazar
    // =========================
    [HttpPost("{id}/Responder")]
    public async Task<IActionResult> Responder(long id, ProveedorProyectoRespuestaRequest model)
    {
        var proyecto = await _db.ProveedorProyectos.FindAsync(id);

        if (proyecto == null)
            return BadRequest("Proyecto inválido.");

        proyecto.EstatusProyectoProveedorId = model.EstatusProyectoProveedorId;
        proyecto.Observaciones = model.Observaciones;
        proyecto.FechaRespuesta = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok("Respuesta registrada.");
    }

    // =========================
    // Listar por proveedor
    // =========================
    [HttpGet("Proveedor/{proveedorId}")]
    public async Task<IActionResult> GetByProveedor(int proveedorId)
    {
        var data = await _db.ProveedorProyectos
            .Where(x => x.ProveedorId == proveedorId && !x.IsDeleted)
            .ToListAsync();

        return Ok(data);
    }
}