using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Clientes.Requests;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de Clientes.
///
/// Funcionalidades:
/// - Crear cliente.
/// - Listar clientes.
/// - Consultar cliente por Id.
/// - Actualizar cliente.
/// - Cambiar estatus (Activar, Suspender, Baja).
///
/// Reglas de negocio:
/// - El correo electrónico es obligatorio y único.
/// - No se permiten clientes duplicados por correo.
/// - Se implementa soft delete mediante IsDeleted.
/// - Se registra auditoría básica (CreatedBy, ModifiedBy, DateCreated, DateModified).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public ClientesController(AppDbContext db) => _db = db;

    // =========================================================
    // POST api/Clientes/CreateCliente
    // =========================================================

    /// <summary>
    /// Crea un nuevo cliente.
    /// 
    /// Validaciones:
    /// - CorreoContacto es obligatorio.
    /// - No puede existir otro cliente activo con el mismo correo.
    /// </summary>
    /// <param name="model">Datos del cliente.</param>
    /// <returns>Id del cliente creado.</returns>
    [HttpPost("CreateCliente")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> CreateCliente([FromBody] ClienteCreateRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        var email = (model.CorreoContacto ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "CorreoContacto es obligatorio.",
                statusCode = 400
            });

        var existe = await _db.Clientes.AsNoTracking()
            .AnyAsync(x => x.CorreoContacto == email && x.IsDeleted == false);

        if (existe)
            return Conflict(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Ya existe un cliente con ese correo.",
                statusCode = 409
            });

        var entity = new Cliente
        {
            RFC = model.RFC?.Trim(),
            RazonSocial = model.RazonSocial?.Trim(),
            NombreComercial = model.NombreComercial?.Trim(),
            CorreoContacto = email,
            TelefonoContacto = model.TelefonoContacto?.Trim(),
            EstatusClienteId = 1, // Activo por defecto
            DateCreated = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name ?? "SYSTEM",
            IsDeleted = false
        };

        _db.Clientes.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Cliente creado.",
            statusCode = 200,
            data = new { entity.ClienteId }
        });
    }

    // =========================================================
    // GET api/Clientes/List
    // =========================================================

    /// <summary>
    /// Lista todos los clientes activos (no eliminados).
    /// </summary>
    [HttpGet("List")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> List()
    {
        var requestId = Guid.NewGuid().ToString();

        var data = await _db.Clientes.AsNoTracking()
            .Where(x => x.IsDeleted == false)
            .OrderByDescending(x => x.ClienteId)
            .Select(x => new
            {
                x.ClienteId,
                x.RazonSocial,
                x.NombreComercial,
                x.CorreoContacto,
                x.EstatusClienteId
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
    // GET api/Clientes/{clienteId}
    // =========================================================

    /// <summary>
    /// Obtiene un cliente por su identificador.
    /// </summary>
    [HttpGet("{clienteId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Get(int clienteId)
    {
        var requestId = Guid.NewGuid().ToString();

        var data = await _db.Clientes.AsNoTracking()
            .Where(x => x.ClienteId == clienteId && x.IsDeleted == false)
            .Select(x => new
            {
                x.ClienteId,
                x.RFC,
                x.RazonSocial,
                x.NombreComercial,
                x.CorreoContacto,
                x.TelefonoContacto,
                x.EstatusClienteId
            })
            .FirstOrDefaultAsync();

        if (data == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Cliente no encontrado.",
                statusCode = 404
            });

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
    // PUT api/Clientes/{clienteId}
    // =========================================================

    /// <summary>
    /// Actualiza los datos de un cliente existente.
    ///
    /// Validaciones:
    /// - El cliente debe existir.
    /// - El correo no puede estar duplicado en otro cliente.
    /// </summary>
    [HttpPut("{clienteId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        int clienteId,
        [FromBody] ClienteUpdateRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        var entity = await _db.Clientes
            .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

        if (entity == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Cliente no encontrado.",
                statusCode = 404
            });

        var email = (model.CorreoContacto ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "CorreoContacto es obligatorio.",
                statusCode = 400
            });

        var emailDuplicado = await _db.Clientes.AsNoTracking()
            .AnyAsync(x => x.ClienteId != clienteId
                        && x.CorreoContacto == email
                        && x.IsDeleted == false);

        if (emailDuplicado)
            return Conflict(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Ese correo ya está registrado en otro cliente.",
                statusCode = 409
            });

        entity.RFC = model.RFC?.Trim();
        entity.RazonSocial = model.RazonSocial?.Trim();
        entity.NombreComercial = model.NombreComercial?.Trim();
        entity.CorreoContacto = email;
        entity.TelefonoContacto = model.TelefonoContacto?.Trim();
        entity.DateModified = DateTime.UtcNow;
        entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Cliente actualizado.",
            statusCode = 200
        });
    }

    // =========================================================
    // Cambios de estatus
    // =========================================================

    /// <summary>
    /// Activa un cliente.
    /// </summary>
    [HttpPost("{clienteId:int}/Activar")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Activar(int clienteId)
        => await SetEstatus(clienteId, 1, "Cliente activado.");

    /// <summary>
    /// Suspende un cliente.
    /// </summary>
    [HttpPost("{clienteId:int}/Suspender")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Suspender(int clienteId)
        => await SetEstatus(clienteId, 2, "Cliente suspendido.");

    /// <summary>
    /// Da de baja un cliente.
    /// </summary>
    [HttpPost("{clienteId:int}/Baja")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Baja(int clienteId)
        => await SetEstatus(clienteId, 3, "Cliente dado de baja.");

    /// <summary>
    /// Método privado para cambiar el estatus del cliente.
    /// </summary>
    private async Task<ActionResult<ApiResponse<object>>> SetEstatus(
        int clienteId,
        int estatus,
        string msg)
    {
        var requestId = Guid.NewGuid().ToString();

        var entity = await _db.Clientes
            .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

        if (entity == null)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Cliente no encontrado.",
                statusCode = 404
            });

        entity.EstatusClienteId = estatus;
        entity.DateModified = DateTime.UtcNow;
        entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

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