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
/// - Eliminar cliente de forma lógica.
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
    private readonly ILogger<ClientesController> _logger;

    /// <summary>
    /// Constructor con inyección del DbContext y logger.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    /// <param name="logger">Logger del controlador.</param>
    public ClientesController(AppDbContext db, ILogger<ClientesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // =========================================================
    // POST api/Clientes/CreateCliente
    // =========================================================

    /// <summary>
    /// Crea un nuevo cliente.
    ///
    /// Validaciones:
    /// - CorreoContacto es obligatorio.
    /// - No puede existir otro cliente activo con el mismo correo.
    /// - El cliente se crea con estatus Activo por defecto.
    /// </summary>
    /// <param name="model">Datos del cliente.</param>
    /// <returns>Cliente creado.</returns>
    [HttpPost("CreateCliente")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> CreateCliente([FromBody] ClienteCreateRequest model)
    {
        

        try
        {
            if (model == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            var email = (model.CorreoContacto ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "CorreoContacto es obligatorio.",
                    statusCode = 400
                });
            }

            var existe = await _db.Clientes
                .AsNoTracking()
                .AnyAsync(x => x.CorreoContacto == email && x.IsDeleted == false);

            if (existe)
            {
                return Conflict(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Ya existe un cliente con ese correo.",
                    statusCode = 409
                });
            }

            // 1 = Activo por defecto
            const int estatusActivo = 1;

            var existeEstatus = await _db.EstatusClientes
                .AsNoTracking()
                .AnyAsync(x => x.EstatusClienteId == estatusActivo);

            if (!existeEstatus)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "No existe el estatus activo configurado en catálogo.",
                    statusCode = 400
                });
            }

            var entity = new Cliente
            {
                RFC = model.RFC?.Trim(),
                RazonSocial = model.RazonSocial?.Trim(),
                NombreComercial = model.NombreComercial?.Trim(),
                CorreoContacto = email,
                TelefonoContacto = model.TelefonoContacto?.Trim(),
                EstatusClienteId = estatusActivo,
                DateCreated = DateTime.UtcNow,
                CreatedBy = User?.Identity?.Name ?? "SYSTEM",
                IsDeleted = false
            };

            _db.Clientes.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Cliente creado.",
                statusCode = 200,
                data = MapCliente(entity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear cliente.");

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al crear cliente.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    // =========================================================
    // GET api/Clientes/List
    // =========================================================

    /// <summary>
    /// Lista todos los clientes no eliminados.
    /// </summary>
    [HttpGet("List")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> List()
    {

        try
        {
            var data = await _db.Clientes
     .AsNoTracking()
     .Where(x => x.IsDeleted == false)
     .OrderByDescending(x => x.ClienteId)
     .Select(x => new
     {
         x.ClienteId,
         x.RFC,
         x.RazonSocial,
         x.NombreComercial,
         x.CorreoContacto,
         x.TelefonoContacto,
         x.EstatusClienteId,
         x.CreatedBy,
         x.ModifiedBy,
         x.DateCreated,
         x.DateModified
     })
     .ToListAsync();
            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Consulta exitosa.",
                statusCode = 200,
                data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar clientes.");

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al consultar clientes.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    // =========================================================
    // GET api/Clientes/{clienteId}
    // =========================================================

    /// <summary>
    /// Obtiene un cliente por su identificador.
    /// </summary>
    [HttpGet("{clienteId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Get(int clienteId)
    {
        

        try
        {
            var entity = await _db.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Cliente no encontrado.",
                    statusCode = 404
                });
            }

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Consulta exitosa.",
                statusCode = 200,
                data = MapCliente(entity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar cliente {ClienteId}.", clienteId);

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al consultar cliente.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
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
    ///
    /// Nota:
    /// - Este método no cambia el estatus.
    /// - El estatus se cambia únicamente con los endpoints Activar, Suspender y Baja.
    /// </summary>
    [HttpPut("{clienteId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        int clienteId,
        [FromBody] ClienteUpdateRequest model)
    {
        

        try
        {
            if (model == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            var entity = await _db.Clientes
                .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Cliente no encontrado.",
                    statusCode = 404
                });
            }

            var email = (model.CorreoContacto ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "CorreoContacto es obligatorio.",
                    statusCode = 400
                });
            }

            var emailDuplicado = await _db.Clientes
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ClienteId != clienteId &&
                    x.CorreoContacto == email &&
                    x.IsDeleted == false);

            if (emailDuplicado)
            {
                return Conflict(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Ese correo ya está registrado en otro cliente.",
                    statusCode = 409
                });
            }

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
                
                success = true,
                message = "Cliente actualizado.",
                statusCode = 200,
                data = MapCliente(entity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar cliente {ClienteId}.", clienteId);

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al actualizar cliente.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    // =========================================================
    // DELETE api/Clientes/{clienteId}
    // =========================================================

    /// <summary>
    /// Elimina un cliente de forma lógica.
    /// </summary>
    /// <param name="clienteId">Identificador del cliente.</param>
    /// <returns>Resultado de la operación.</returns>
    [HttpDelete("{clienteId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int clienteId)
    {
        

        try
        {
            var entity = await _db.Clientes
                .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Cliente no encontrado.",
                    statusCode = 404
                });
            }

            entity.IsDeleted = true;
            entity.DateModified = DateTime.UtcNow;
            entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Cliente eliminado correctamente.",
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar cliente {ClienteId}.", clienteId);

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al eliminar cliente.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    // =========================================================
    // Cambios de estatus
    // =========================================================

    /// <summary>
    /// Activa un cliente.
    /// </summary>
    [HttpPost("{clienteId:int}/Activar")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Activar(int clienteId)
        => await SetEstatus(clienteId, 1, "Cliente activado.");

    /// <summary>
    /// Suspende un cliente.
    /// </summary>
    [HttpPost("{clienteId:int}/Suspender")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Suspender(int clienteId)
        => await SetEstatus(clienteId, 2, "Cliente suspendido.");

    /// <summary>
    /// Da de baja un cliente.
    /// </summary>
    [HttpPost("{clienteId:int}/Baja")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
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
        

        try
        {
            var entity = await _db.Clientes
                .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Cliente no encontrado.",
                    statusCode = 404
                });
            }

            var existeEstatus = await _db.EstatusClientes
                .AsNoTracking()
                .AnyAsync(x => x.EstatusClienteId == estatus);

            if (!existeEstatus)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "El estatus solicitado no es válido.",
                    statusCode = 400
                });
            }

            entity.EstatusClienteId = estatus;
            entity.DateModified = DateTime.UtcNow;
            entity.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = msg,
                statusCode = 200,
                data = new
                {
                    entity.ClienteId,
                    entity.EstatusClienteId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus del cliente {ClienteId}.", clienteId);

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al cambiar estatus del cliente.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    /// <summary>
    /// Convierte la entidad Cliente en un objeto de respuesta.
    /// </summary>
    private static object MapCliente(Cliente entity) => new
    {
        entity.ClienteId,
        entity.RFC,
        entity.RazonSocial,
        entity.NombreComercial,
        entity.CorreoContacto,
        entity.TelefonoContacto,
        entity.EstatusClienteId,
        entity.CreatedBy,
        entity.ModifiedBy,
        entity.DateCreated,
        entity.DateModified
    };

    /// <summary>
    /// Obtiene la lista completa de mensajes de error, incluyendo InnerException.
    /// </summary>
    private static List<string> GetErrorMessages(Exception ex)
    {
        var errors = new List<string>();
        var current = ex;

        while (current != null)
        {
            errors.Add(current.Message);
            current = current.InnerException;
        }

        return errors;
    }
}