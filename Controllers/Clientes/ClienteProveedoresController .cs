using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Clientes.Requests;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de relaciones entre Clientes y Proveedores.
///
/// Funcionalidades:
/// - Dar de alta relación cliente-proveedor.
/// - Aprobar proveedor para un cliente.
/// - Suspender proveedor.
/// - Dar de baja proveedor.
/// - Actualizar notas de la relación.
/// - Listar proveedores asociados a un cliente.
///
/// Reglas de negocio:
/// - El cliente debe existir y estar ACTIVO.
/// - El proveedor debe existir.
/// - Para aprobar, el proveedor debe estar ACTIVO globalmente.
/// - Se implementa soft delete mediante IsDeleted.
/// - Se mantiene auditoría (CreatedBy, ModifiedBy, DateCreated, DateModified).
/// </summary>
[ApiController]
[Route("api/Clientes/{clienteId:int}/Proveedores")]
public class ClienteProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    public ClienteProveedoresController(AppDbContext db) => _db = db;

    // =========================================================
    // POST /api/Clientes/{clienteId}/Proveedores/{proveedorId}/Alta
    // =========================================================

    /// <summary>
    /// Crea la relación entre un cliente y un proveedor.
    /// Por defecto la relación queda en estado SUSPENDIDO (2) hasta aprobación.
    /// </summary>
    [HttpPost("{proveedorId:int}/Alta")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Alta(int clienteId, int proveedorId)
    {
        var requestId = Guid.NewGuid().ToString();

        var cliente = await _db.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

        if (cliente == null || cliente.EstatusClienteId != 1)
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Cliente inválido o no activo.",
                statusCode = 400
            });

        var proveedorOk = await _db.Proveedores.AsNoTracking()
            .AnyAsync(p => p.ProveedorId == proveedorId && p.IsDeleted != true);

        if (!proveedorOk)
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Proveedor no encontrado.",
                statusCode = 400
            });

        var rel = await _db.ClienteProveedor.FirstOrDefaultAsync(x =>
            x.ClienteId == clienteId && x.ProveedorId == proveedorId && x.IsDeleted == false);

        if (rel != null)
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Relación ya existe.",
                statusCode = 200,
                data = new { rel.ClienteProveedorId }
            });

        rel = new ClienteProveedor
        {
            ClienteId = clienteId,
            ProveedorId = proveedorId,
            EstatusRelacionId = 2, // 2 = SUSPENDIDO
            CreatedBy = User?.Identity?.Name ?? "SYSTEM",
            DateCreated = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.ClienteProveedor.Add(rel);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Relación creada.",
            statusCode = 200,
            data = new { rel.ClienteProveedorId }
        });
    }

    // =========================================================
    // Cambios de estatus
    // =========================================================

    /// <summary>
    /// Aprueba un proveedor para el cliente.
    /// </summary>
    [HttpPost("{proveedorId:int}/Aprobar")]
    [Authorize]
    public Task<ActionResult<ApiResponse<object>>> Aprobar(int clienteId, int proveedorId)
        => SetRelacionEstatus(clienteId, proveedorId, 1, "Proveedor aprobado para el cliente.");

    /// <summary>
    /// Suspende la relación cliente-proveedor.
    /// </summary>
    [HttpPost("{proveedorId:int}/Suspender")]
    [Authorize]
    public Task<ActionResult<ApiResponse<object>>> Suspender(int clienteId, int proveedorId)
        => SetRelacionEstatus(clienteId, proveedorId, 2, "Proveedor suspendido para el cliente.");

    /// <summary>
    /// Da de baja la relación cliente-proveedor.
    /// </summary>
    [HttpPost("{proveedorId:int}/Baja")]
    [Authorize]
    public Task<ActionResult<ApiResponse<object>>> Baja(int clienteId, int proveedorId)
        => SetRelacionEstatus(clienteId, proveedorId, 3, "Proveedor dado de baja para el cliente.");

    // =========================================================
    // PUT Notas
    // =========================================================

    /// <summary>
    /// Actualiza las notas asociadas a la relación cliente-proveedor.
    /// </summary>
    [HttpPut("{proveedorId:int}/Notas")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> UpdateNotas(
        int clienteId,
        int proveedorId,
        [FromBody] ClienteProveedorNotasRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        var rel = await _db.ClienteProveedor.FirstOrDefaultAsync(x =>
            x.ClienteId == clienteId && x.ProveedorId == proveedorId && x.IsDeleted == false);

        if (rel == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Relación no encontrada.",
                statusCode = 404
            });

        rel.Notas = model.Notas;
        rel.DateModified = DateTime.UtcNow;
        rel.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Notas actualizadas.",
            statusCode = 200
        });
    }

    // =========================================================
    // GET List
    // =========================================================

    /// <summary>
    /// Lista los proveedores asociados a un cliente.
    /// </summary>
    [HttpGet("List")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> List(int clienteId)
    {
        var requestId = Guid.NewGuid().ToString();

        var data = await _db.ClienteProveedor.AsNoTracking()
            .Where(x => x.ClienteId == clienteId && x.IsDeleted == false)
            .Select(x => new
            {
                x.ClienteProveedorId,
                x.ProveedorId,
                x.EstatusRelacionId,
                x.Notas
            })
            .ToListAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "OK",
            statusCode = 200,
            data = data
        });
    }

    // =========================================================
    // Método privado para cambiar estatus
    // =========================================================

    /// <summary>
    /// Cambia el estatus de la relación cliente-proveedor.
    /// Regla especial: Para aprobar (1), el proveedor debe estar ACTIVO globalmente.
    /// </summary>
    private async Task<ActionResult<ApiResponse<object>>> SetRelacionEstatus(
        int clienteId,
        int proveedorId,
        int estatusRelacionId,
        string msg)
    {
        var requestId = Guid.NewGuid().ToString();

        var rel = await _db.ClienteProveedor.FirstOrDefaultAsync(x =>
            x.ClienteId == clienteId && x.ProveedorId == proveedorId && x.IsDeleted == false);

        if (rel == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Relación no encontrada. Primero da de alta.",
                statusCode = 404
            });

        if (estatusRelacionId == 1)
        {
            var proveedorActivo = await _db.Proveedores.AsNoTracking()
                .AnyAsync(p => p.ProveedorId == proveedorId
                            && p.EstatusProveedorId == 1
                            && p.IsDeleted != true);

            if (!proveedorActivo)
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El proveedor no está ACTIVO globalmente.",
                    statusCode = 400
                });
        }

        rel.EstatusRelacionId = estatusRelacionId;
        rel.DateModified = DateTime.UtcNow;
        rel.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = msg,
            statusCode = 200
        });
    }
}