using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/Presupuestos")]
public class PresupuestosController : ControllerBase
{
    private readonly AppDbContext _db;
    public PresupuestosController(AppDbContext db) => _db = db;

    private const int ENVIADO = 1;
    private const int APROBADO = 2;
    private const int RECHAZADO = 3;

    // =========================
    // POST /api/Presupuestos/Enviar
    // =========================
    [HttpPost("Enviar")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Enviar([FromBody] PresupuestoCreateRequest model)
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

            // validar proyecto existe
            var proyecto = await _db.ProveedorProyectos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProveedorProyectoId == model.ProveedorProyectoId && !x.IsDeleted);

            if (proyecto == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proyecto inválido.",
                    statusCode = 400
                });
            }

            // validar proveedor coincide con proyecto
            if (proyecto.ProveedorId != model.ProveedorId)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proveedor no corresponde al proyecto.",
                    statusCode = 400
                });
            }

            // validar no exista uno "enviado" abierto (índice también lo impone)
            var existeEnviado = await _db.ProveedorPresupuestos
                .AnyAsync(x => x.ProveedorProyectoId == model.ProveedorProyectoId
                            && x.EstatusPresupuestoId == ENVIADO
                            && !x.IsDeleted);

            if (existeEnviado)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Ya existe un presupuesto ENVIADO pendiente para este proyecto.",
                    statusCode = 400
                });
            }

            // versión = max + 1
            var maxVersion = await _db.ProveedorPresupuestos.AsNoTracking()
                .Where(x => x.ProveedorProyectoId == model.ProveedorProyectoId && !x.IsDeleted)
                .Select(x => (int?)x.Version)
                .MaxAsync() ?? 0;

            var entity = new ProveedorPresupuesto
            {
                ProveedorProyectoId = model.ProveedorProyectoId,
                ProveedorId = model.ProveedorId,
                Version = maxVersion + 1,
                Monto = model.Monto,
                Moneda = string.IsNullOrWhiteSpace(model.Moneda) ? "MXN" : model.Moneda!,
                Descripcion = model.Descripcion,

                EstatusPresupuestoId = ENVIADO,
                FechaEnvio = DateTime.UtcNow,
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ProveedorPresupuestos.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Presupuesto enviado.",
                statusCode = 200,
                data = new { entity.PresupuestoId, entity.Version }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al enviar presupuesto.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================
    // POST /api/Presupuestos/{presupuestoId}/Decision
    // =========================
    [HttpPost("{presupuestoId:long}/Decision")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Decision(long presupuestoId, [FromBody] PresupuestoDecisionRequest model)
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

            if (model.EstatusPresupuestoId != APROBADO && model.EstatusPresupuestoId != RECHAZADO)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "EstatusPresupuestoId inválido. Use 2 (Aprobado) o 3 (Rechazado).",
                    statusCode = 400
                });
            }

            var entity = await _db.ProveedorPresupuestos
                .FirstOrDefaultAsync(x => x.PresupuestoId == presupuestoId && !x.IsDeleted);

            if (entity == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Presupuesto inválido.",
                    statusCode = 400
                });
            }

            if (entity.EstatusPresupuestoId != ENVIADO)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Solo se puede decidir un presupuesto en estado ENVIADO.",
                    statusCode = 400
                });
            }

            entity.EstatusPresupuestoId = model.EstatusPresupuestoId;
            entity.MotivoDecision = model.MotivoDecision?.Trim();
            entity.FechaDecision = DateTime.UtcNow;
            entity.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = model.EstatusPresupuestoId == APROBADO ? "Presupuesto aprobado." : "Presupuesto rechazado.",
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al decidir presupuesto.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================
    // GET /api/Presupuestos/Proyecto/{proveedorProyectoId}
    // =========================
    [HttpGet("Proyecto/{proveedorProyectoId:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetByProyecto(long proveedorProyectoId)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            var items = await _db.ProveedorPresupuestos.AsNoTracking()
                .Where(x => x.ProveedorProyectoId == proveedorProyectoId && !x.IsDeleted)
                .OrderByDescending(x => x.Version)
                .Select(x => new
                {
                    x.PresupuestoId,
                    x.Version,
                    x.Monto,
                    x.Moneda,
                    x.Descripcion,
                    x.EstatusPresupuestoId,
                    x.FechaEnvio,
                    x.FechaDecision,
                    x.MotivoDecision
                })
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
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al consultar presupuestos.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }
}