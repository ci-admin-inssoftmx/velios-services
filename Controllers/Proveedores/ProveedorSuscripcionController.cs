using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/Proveedores/{proveedorId:int}/Suscripcion")]
public class ProveedorSuscripcionController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProveedorSuscripcionController(AppDbContext db) => _db = db;

    private const int ESTATUS_ACTIVA = 1;
    private const int ESTATUS_SUSPENDIDA = 2;
    private const int ESTATUS_CANCELADA = 3;

    // =========================
    // POST /api/Proveedores/{proveedorId}/Suscripcion
    // Alta o Cambio de paquete
    // =========================
    [HttpPost]
    [AllowAnonymous] // lo dejaste público en módulo 1
    public async Task<ActionResult<ApiResponse<object>>> Upsert(int proveedorId, [FromBody] ProveedorSuscripcionUpsertRequest model)
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

            var existeProveedor = await _db.Proveedores.AsNoTracking().AnyAsync(p => p.ProveedorId == proveedorId && p.IsDeleted != true);    
            if (!existeProveedor)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proveedor inválido.",
                    statusCode = 400
                });
            }

            var paqueteValido = await _db.CatPaquetes.AsNoTracking()
                .AnyAsync(p => p.PaqueteId == model.PaqueteId && p.Activo && !p.IsDeleted);

            if (!paqueteValido)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Paquete inválido o inactivo.",
                    statusCode = 400
                });
            }

            var hoy = DateTime.UtcNow.Date;
            var fechaInicio = (model.FechaInicio ?? hoy).Date;

            // Busca activa actual
            var activa = await _db.ProveedorSuscripciones
                .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId && x.EstatusSuscripcionId == ESTATUS_ACTIVA && !x.IsDeleted);

            // Si existe activa y es el mismo paquete -> no hacer nada
            if (activa != null && activa.PaqueteId == model.PaqueteId)
            {
                return Ok(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = true,
                    message = "El proveedor ya tiene ese paquete activo.",
                    statusCode = 200,
                    data = new { activa.ProveedorSuscripcionId }
                });
            }

            // Si existe activa, cancelarla (histórico)
            if (activa != null)
            {
                activa.EstatusSuscripcionId = ESTATUS_CANCELADA;
                activa.FechaFin = hoy;
                activa.DateModified = DateTime.UtcNow;
                activa.ModifiedBy = "API";
            }

            // Crear nueva activa
            var nueva = new ProveedorSuscripcion
            {
                ProveedorId = proveedorId,
                PaqueteId = model.PaqueteId,
                EstatusSuscripcionId = ESTATUS_ACTIVA,
                FechaInicio = fechaInicio,
                FechaFin = null,
                Motivo = null,

                DateCreated = DateTime.UtcNow,
                CreatedBy = "API",
                IsDeleted = false
            };

            _db.ProveedorSuscripciones.Add(nueva);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Suscripción actualizada con éxito.",
                statusCode = 200,
                data = new { nueva.ProveedorSuscripcionId }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al actualizar suscripción.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================
    // POST /api/Proveedores/{proveedorId}/Suscripcion/Suspend
    // Suspender suscripción activa
    // =========================
    [HttpPost("Suspend")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Suspend(int proveedorId, [FromBody] ProveedorSuscripcionSuspendRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            var activa = await _db.ProveedorSuscripciones
                .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId && x.EstatusSuscripcionId == ESTATUS_ACTIVA && !x.IsDeleted);

            if (activa == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "No existe suscripción activa para suspender.",
                    statusCode = 400
                });
            }

            activa.EstatusSuscripcionId = ESTATUS_SUSPENDIDA;
            activa.Motivo = model.Motivo?.Trim();
            activa.DateModified = DateTime.UtcNow;
            activa.ModifiedBy = "API";

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Suscripción suspendida.",
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al suspender suscripción.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================
    // GET /api/Proveedores/{proveedorId}/Suscripcion
    // Devuelve activa + historial
    // =========================
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Get(int proveedorId)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            var items = await _db.ProveedorSuscripciones.AsNoTracking()
                .Where(x => x.ProveedorId == proveedorId && !x.IsDeleted)
                .OrderByDescending(x => x.ProveedorSuscripcionId)
                .Select(x => new
                {
                    x.ProveedorSuscripcionId,
                    x.PaqueteId,
                    x.EstatusSuscripcionId,
                    x.FechaInicio,
                    x.FechaFin,
                    x.Motivo
                })
                .ToListAsync();

            var activa = items.FirstOrDefault(x => x.EstatusSuscripcionId == ESTATUS_ACTIVA);

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Solicitud ejecutada con éxito.",
                statusCode = 200,
                data = new { activa, historial = items }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al consultar suscripción.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }
}