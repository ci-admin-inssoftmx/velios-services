using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Clientes.Requests;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de Proveedores asignados a un Proyecto.
///
/// Funcionalidades:
/// - Asignar proveedor a un proyecto.
/// - Reasignar tipo de servicio.
/// - Desasignar proveedor.
/// - Cambiar tipo de servicio.
///
/// Reglas de negocio:
/// - El proyecto debe existir y estar ACTIVO.
/// - El proveedor debe estar APROBADO para el cliente del proyecto.
/// - El TipoServicio debe existir en CatTipoServicio.
/// - Se implementa soft delete mediante IsDeleted.
/// - Se mantiene auditoría básica (CreatedBy, ModifiedBy, DateCreated, DateModified).
/// </summary>
[ApiController]
[Route("api/Proyectos/{proyectoId:int}/Proveedores")]
public class ProyectoProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    public ProyectoProveedoresController(AppDbContext db) => _db = db;

    // =========================================================
    // POST /api/Proyectos/{proyectoId}/Proveedores/Asignar
    // =========================================================

    /// <summary>
    /// Asigna un proveedor a un proyecto.
    ///
    /// Validaciones:
    /// - El proyecto debe existir y estar activo.
    /// - El proveedor debe estar aprobado para el cliente.
    /// - El TipoServicio debe existir.
    /// 
    /// Si la asignación ya existe, se reactiva y actualiza el tipo de servicio.
    /// </summary>
    [HttpPost("Asignar")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Asignar(
        int proyectoId,
        [FromBody] AsignarProveedorProyectoRequest model)
    {
        

        var proyecto = await _db.ClienteProyectos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProyectoId == proyectoId && p.IsDeleted == false);

        if (proyecto == null)
            return NotFound(new ApiResponse<object>
            {
                
                success = false,
                message = "Proyecto no encontrado.",
                statusCode = 404
            });

        if (proyecto.EstatusProyectoId != 1)
            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Proyecto no está activo.",
                statusCode = 400
            });

        // Validar proveedor aprobado para el cliente
        var aprobado = await _db.ClienteProveedor.AsNoTracking().AnyAsync(r =>
            r.ClienteId == proyecto.ClienteId &&
            r.ProveedorId == model.ProveedorId &&
            r.EstatusRelacionId == 1 &&
            r.IsDeleted == false);

        if (!aprobado)
            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "El proveedor no está aprobado para este cliente.",
                statusCode = 400
            });

        // Validar TipoServicio existente
        var tipoServicioOk = await _db.Set<dynamic>()
            .FromSqlInterpolated($@"SELECT TipoServicioId FROM dbo.CatTipoServicio WHERE TipoServicioId = {model.TipoServicioId}")
            .AnyAsync();

        if (!tipoServicioOk)
            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "TipoServicioId inválido.",
                statusCode = 400
            });

        var existe = await _db.ClienteProyectoProveedores
            .FirstOrDefaultAsync(x =>
                x.ProyectoId == proyectoId &&
                x.ProveedorId == model.ProveedorId &&
                x.IsDeleted == false);

        if (existe != null)
        {
            existe.TipoServicioId = model.TipoServicioId;
            existe.ActivoAsignacion = true;
            existe.DateModified = DateTime.UtcNow;
            existe.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Proveedor reasignado al proyecto.",
                statusCode = 200
            });
        }

        var entity = new ClienteProyectoProveedor
        {
            ProyectoId = proyectoId,
            ProveedorId = model.ProveedorId,
            TipoServicioId = model.TipoServicioId,
            ActivoAsignacion = true,
            CreatedBy = User?.Identity?.Name ?? "SYSTEM",
            DateCreated = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.ClienteProyectoProveedores.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            
            success = true,
            message = "Proveedor asignado al proyecto.",
            statusCode = 200,
            data = new { entity.ClienteProyectoProveedorId }
        });
    }

    // =========================================================
    // POST /api/Proyectos/{proyectoId}/Proveedores/Desasignar
    // =========================================================

    /// <summary>
    /// Desactiva la asignación de un proveedor en un proyecto.
    /// </summary>
    [HttpPost("Desasignar")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Desasignar(
        int proyectoId,
        [FromBody] AsignarProveedorProyectoRequest model)
    {
        

        var entity = await _db.ClienteProyectoProveedores
            .FirstOrDefaultAsync(x =>
                x.ProyectoId == proyectoId &&
                x.ProveedorId == model.ProveedorId &&
                x.IsDeleted == false);

        if (entity == null)
            return NotFound(new ApiResponse<object>
            {
                
                success = false,
                message = "Asignación no encontrada.",
                statusCode = 404
            });

        entity.ActivoAsignacion = false;
        entity.DateModified = DateTime.UtcNow;
        entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            
            success = true,
            message = "Proveedor desasignado.",
            statusCode = 200
        });
    }

    // =========================================================
    // PUT /api/Proyectos/{proyectoId}/Proveedores/CambiarTipoServicio
    // =========================================================

    /// <summary>
    /// Cambia el tipo de servicio asignado a un proveedor en un proyecto.
    /// </summary>
    [HttpPut("CambiarTipoServicio")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> CambiarTipoServicio(
        int proyectoId,
        [FromBody] AsignarProveedorProyectoRequest model)
    {
        

        var entity = await _db.ClienteProyectoProveedores
            .FirstOrDefaultAsync(x =>
                x.ProyectoId == proyectoId &&
                x.ProveedorId == model.ProveedorId &&
                x.IsDeleted == false);

        if (entity == null)
            return NotFound(new ApiResponse<object>
            {
                
                success = false,
                message = "Asignación no encontrada.",
                statusCode = 404
            });

        entity.TipoServicioId = model.TipoServicioId;
        entity.DateModified = DateTime.UtcNow;
        entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            
            success = true,
            message = "Tipo de servicio actualizado.",
            statusCode = 200
        });
    }
}