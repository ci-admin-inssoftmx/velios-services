using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/ProyectoTrabajadores")]
public class ProyectoTrabajadoresController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProyectoTrabajadoresController(AppDbContext db) => _db = db;

    [HttpPost("Asignar")]
    public async Task<ActionResult<ApiResponse<object>>> Asignar([FromBody] ProyectoTrabajadorAssignRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            // validar proyecto existe y proveedor coincide
            var proyecto = await _db.ProveedorProyectos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProveedorProyectoId == model.ProveedorProyectoId && !x.IsDeleted);

            if (proyecto == null || proyecto.ProveedorId != model.ProveedorId)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proyecto inválido o proveedor no corresponde.",
                    statusCode = 400
                });
            }

            // validar trabajador existe y pertenece al proveedor
            var trabajador = await _db.ProveedorTrabajadores.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TrabajadorId == model.TrabajadorId && !x.IsDeleted);

            if (trabajador == null || trabajador.ProveedorId != model.ProveedorId)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Trabajador inválido o no pertenece al proveedor.",
                    statusCode = 400
                });
            }

            // anti-duplicado (índice también lo impone)
            var yaAsignado = await _db.ProveedorProyectoTrabajadores
                .AnyAsync(x => x.ProveedorProyectoId == model.ProveedorProyectoId
                            && x.TrabajadorId == model.TrabajadorId
                            && !x.IsDeleted);

            if (yaAsignado)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El trabajador ya está asignado a este proyecto.",
                    statusCode = 400
                });
            }

            var entity = new ProveedorProyectoTrabajador
            {
                ProveedorProyectoId = model.ProveedorProyectoId,
                ProveedorId = model.ProveedorId,
                TrabajadorId = model.TrabajadorId,
                FechaAsignacion = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ProveedorProyectoTrabajadores.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Trabajador asignado al proyecto.",
                statusCode = 200,
                data = new { entity.ProveedorProyectoTrabajadorId }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al asignar trabajador.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    [HttpGet("Proyecto/{proveedorProyectoId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetByProyecto(long proveedorProyectoId)
    {
        var requestId = Guid.NewGuid().ToString();

        var items = await _db.ProveedorProyectoTrabajadores.AsNoTracking()
            .Where(x => x.ProveedorProyectoId == proveedorProyectoId && !x.IsDeleted)
            .Join(_db.ProveedorTrabajadores.AsNoTracking().Where(t => !t.IsDeleted),
                  a => a.TrabajadorId,
                  t => t.TrabajadorId,
                  (a, t) => new
                  {
                      a.ProveedorProyectoTrabajadorId,
                      a.TrabajadorId,
                      t.Nombre,
                      t.ApellidoPaterno,
                      t.ApellidoMaterno,
                      t.CURP,
                      t.RFC,
                      t.NSS,
                      t.Telefono,
                      t.Correo
                  })
            .OrderByDescending(x => x.TrabajadorId)
            .ToListAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Solicitud ejecutada con éxito.",
            statusCode = 200,
            data = new { total = items.Count, items }
        });
    }
}