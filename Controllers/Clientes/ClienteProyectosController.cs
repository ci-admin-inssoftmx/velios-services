using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Clientes.Requests;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de Proyectos pertenecientes a un Cliente.
///
/// Funcionalidades:
/// - Crear proyectos.
/// - Listar proyectos.
/// - Actualizar proyectos activos.
/// - Cerrar proyectos.
///
/// Reglas de negocio:
/// - El cliente debe existir y estar ACTIVO.
/// - Solo proyectos con EstatusProyectoId = 1 (Activo) pueden modificarse.
/// - Se implementa soft delete (IsDeleted).
/// - Se registra auditoría básica (CreatedBy, ModifiedBy, DateCreated, DateModified).
/// </summary>
[ApiController]
[Route("api/Clientes/{clienteId:int}/Proyectos")]
public class ClienteProyectosController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public ClienteProyectosController(AppDbContext db) => _db = db;

    // =========================================================
    // POST /api/Clientes/{clienteId}/Proyectos/Create
    // =========================================================

    /// <summary>
    /// Crea un nuevo proyecto para un cliente activo.
    /// </summary>
    /// <param name="clienteId">Identificador del cliente.</param>
    /// <param name="model">Datos del proyecto.</param>
    /// <returns>Identificador del proyecto creado.</returns>
    [HttpPost("Create")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        int clienteId,
        [FromBody] ProyectoCreateRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        var clienteOk = await _db.Clientes.AsNoTracking()
            .AnyAsync(x => x.ClienteId == clienteId
                        && x.EstatusClienteId == 1
                        && x.IsDeleted == false);

        if (!clienteOk)
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Cliente inválido o no activo.",
                statusCode = 400
            });

        var nombre = (model.Nombre ?? "").Trim();

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Nombre es obligatorio.",
                statusCode = 400
            });

        var entity = new ClienteProyecto
        {
            ClienteId = clienteId,
            Nombre = nombre,
            Descripcion = model.Descripcion?.Trim(),
            FechaInicio = model.FechaInicio,
            FechaFin = model.FechaFin,
            EstatusProyectoId = 1, // Activo
            CreatedBy = User?.Identity?.Name ?? "SYSTEM",
            DateCreated = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.ClienteProyectos.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Proyecto creado.",
            statusCode = 200,
            data = new { entity.ProyectoId }
        });
    }

    // =========================================================
    // GET /api/Clientes/{clienteId}/Proyectos/List
    // =========================================================

    /// <summary>
    /// Lista todos los proyectos activos (no eliminados) de un cliente.
    /// </summary>
    /// <param name="clienteId">Identificador del cliente.</param>
    /// <returns>Listado de proyectos.</returns>
    [HttpGet("List")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> List(int clienteId)
    {
        var requestId = Guid.NewGuid().ToString();

        var data = await _db.ClienteProyectos.AsNoTracking()
            .Where(x => x.ClienteId == clienteId && x.IsDeleted == false)
            .OrderByDescending(x => x.ProyectoId)
            .Select(x => new
            {
                x.ProyectoId,
                x.Nombre,
                x.EstatusProyectoId,
                x.FechaInicio,
                x.FechaFin
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
    // PUT /api/Clientes/{clienteId}/Proyectos/{proyectoId}
    // =========================================================

    /// <summary>
    /// Actualiza un proyecto activo.
    ///
    /// Reglas:
    /// - El proyecto debe pertenecer al cliente.
    /// - Solo proyectos activos pueden modificarse.
    /// </summary>
    /// <param name="clienteId">Identificador del cliente.</param>
    /// <param name="proyectoId">Identificador del proyecto.</param>
    /// <param name="model">Datos actualizados.</param>
    [HttpPut("{proyectoId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        int clienteId,
        int proyectoId,
        [FromBody] ProyectoUpdateRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        var entity = await _db.ClienteProyectos.FirstOrDefaultAsync(x =>
            x.ProyectoId == proyectoId
            && x.ClienteId == clienteId
            && x.IsDeleted == false);

        if (entity == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Proyecto no encontrado.",
                statusCode = 404
            });

        if (entity.EstatusProyectoId != 1)
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Solo puedes modificar proyectos activos.",
                statusCode = 400
            });

        var nombre = (model.Nombre ?? "").Trim();

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Nombre es obligatorio.",
                statusCode = 400
            });

        entity.Nombre = nombre;
        entity.Descripcion = model.Descripcion?.Trim();
        entity.FechaInicio = model.FechaInicio;
        entity.FechaFin = model.FechaFin;
        entity.DateModified = DateTime.UtcNow;
        entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Proyecto actualizado.",
            statusCode = 200
        });
    }

    // =========================================================
    // POST /api/Clientes/{clienteId}/Proyectos/{proyectoId}/Cerrar
    // =========================================================

    /// <summary>
    /// Cierra un proyecto cambiando su estatus a 2 (Cerrado).
    /// </summary>
    /// <param name="clienteId">Identificador del cliente.</param>
    /// <param name="proyectoId">Identificador del proyecto.</param>
    [HttpPost("{proyectoId:int}/Cerrar")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Cerrar(
        int clienteId,
        int proyectoId)
    {
        var requestId = Guid.NewGuid().ToString();

        var entity = await _db.ClienteProyectos.FirstOrDefaultAsync(x =>
            x.ProyectoId == proyectoId
            && x.ClienteId == clienteId
            && x.IsDeleted == false);

        if (entity == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Proyecto no encontrado.",
                statusCode = 404
            });

        entity.EstatusProyectoId = 2; // Cerrado
        entity.DateModified = DateTime.UtcNow;
        entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Proyecto cerrado.",
            statusCode = 200
        });
    }
}