using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Asistencia;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador responsable de la gestión de registros de asistencia.
///
/// Funcionalidades:
/// - Crear registro de asistencia.
/// - Consultar registros por trabajador y rango de fechas.
///
/// Seguridad:
/// - Requiere autenticación JWT.
/// - Aplica validaciones para evitar duplicados.
/// - Implementa soft-delete mediante IsDeleted.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AsistenciaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AsistenciaController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de asistencia.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    /// <param name="logger">Logger para eventos y errores.</param>
    public AsistenciaController(AppDbContext db, ILogger<AsistenciaController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Crea un nuevo registro de asistencia para un trabajador.
    /// </summary>
    /// <param name="model">Modelo de creación de registro.</param>
    /// <returns>Respuesta estándar de la API.</returns>
    [HttpPost("CreateRegistroAsistencia")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> CreateRegistroAsistencia(
        [FromBody] AsistenciaRegistroCreateModel model)
    {
        

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400,
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            var tipoRegistro = (model.TipoRegistro ?? string.Empty).Trim();
            var origen = (model.Origen ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(tipoRegistro))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "El tipo de registro es obligatorio.",
                    statusCode = 400
                });
            }

            if (string.IsNullOrWhiteSpace(origen))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "El origen es obligatorio.",
                    statusCode = 400
                });
            }

            var existeTrabajador = await _db.ProveedorTrabajadores
                .AsNoTracking()
                .AnyAsync(x => x.TrabajadorId == model.TrabajadorId && !x.IsDeleted);

            if (!existeTrabajador)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Trabajador inválido.",
                    statusCode = 400
                });
            }

            var existeDuplicado = await _db.AsistenciaRegistros
                .AsNoTracking()
                .AnyAsync(r =>
                    r.TrabajadorId == model.TrabajadorId &&
                    r.Fecha == model.FechaRegistro.Date &&
                    r.TipoRegistro == tipoRegistro &&
                    r.Origen == origen &&
                    !r.IsDeleted);

            if (existeDuplicado)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Ya existe un registro de asistencia con los mismos datos.",
                    statusCode = 400
                });
            }

            var entity = new AsistenciaRegistro
            {
                TrabajadorId = model.TrabajadorId,
                CentroTrabajoId = model.CentroTrabajoId, // <--- Agregado aquí
                Fecha = model.FechaRegistro.Date,
                HoraEntrada = model.HoraEntrada,
                HoraSalida = model.HoraSalida,
                TipoRegistro = tipoRegistro,
                Origen = origen,
                Latitud = model.Latitud,
                Longitud = model.Longitud,
                Observacion = model.Observacion,
                CreatedBy = "API",
                DateCreated = DateTime.UtcNow,
                ModifiedBy = null,
                DateModified = null,
                IsDeleted = false
            };

            _db.AsistenciaRegistros.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Registro de asistencia creado con éxito.",
                data = new
                {
                    entity.AsistenciaRegistroId,
                    entity.TrabajadorId,
                    entity.CentroTrabajoId, // <--- Opcional: Incluirlo en la respuesta
                    entity.Fecha,
                    // ... resto de los campos
                },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear registro de asistencia.");

            var errors = new List<string>();
            var current = ex;
            while (current != null)
            {
                errors.Add(current.Message);
                current = current.InnerException;
            }

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al crear registro de asistencia.",
                statusCode = 400,
                errors = errors
            });
        }
    }

    /// <summary>
    /// Obtiene los registros de asistencia de un trabajador dentro de un rango de fechas.
    /// </summary>
    /// <param name="model">Modelo de filtros para consulta.</param>
    /// <returns>Listado de registros encontrados.</returns>
    [HttpGet("GetRegistrosAsistencia")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> GetRegistrosAsistencia(
        [FromQuery] AsistenciaRegistroGetModel model)
    {
        

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400,
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            if (model.FechaInicio.Date > model.FechaFin.Date)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "La FechaInicio no puede ser mayor que la FechaFin.",
                    statusCode = 400
                });
            }

            var origen = (model.Origen ?? string.Empty).Trim().ToUpperInvariant();
            var tipoRegistro = (model.TipoRegistro ?? string.Empty).Trim();

            var query = from r in _db.AsistenciaRegistros.AsNoTracking()
                        join ct in _db.CentrosTrabajo.AsNoTracking() on r.CentroTrabajoId equals ct.CentroTrabajoId into ctGroup
                        from ct in ctGroup.DefaultIfEmpty() // Esto es un Left Join
                        where r.TrabajadorId == model.TrabajadorId &&
                              r.Fecha >= model.FechaInicio.Date &&
                              r.Fecha <= model.FechaFin.Date &&
                              !r.IsDeleted
                        select new { r, ct };

            if (!string.IsNullOrWhiteSpace(origen))
            {
                query = query.Where(x => x.r.Origen == origen);
            }

            if (!string.IsNullOrWhiteSpace(tipoRegistro))
            {
                query = query.Where(x => x.r.TipoRegistro == tipoRegistro);
            }

            var data = await query
    .OrderByDescending(x => x.r.Fecha)
    .ThenByDescending(x => x.r.HoraEntrada)
    .Select(x => new
    {
        x.r.AsistenciaRegistroId,
        x.r.TrabajadorId,

        // --- AQUÍ ESTÁ LO NUEVO ---
        centroTrabajoId = x.r.CentroTrabajoId,
        centroTrabajoNombre = x.ct != null ? x.ct.Nombre : "CENTRO NO ASIGNADO",
        // --------------------------

        x.r.Fecha,
        x.r.HoraEntrada,
        x.r.HoraSalida,
        x.r.TipoRegistro,
        x.r.Origen,
        x.r.Latitud,
        x.r.Longitud,
        x.r.Observacion,
        x.r.DateCreated,
        x.r.DateModified
    })
    .ToListAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Consulta exitosa.",
                data = data,
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar registros de asistencia.");

            var errors = new List<string>();
            var current = ex;
            while (current != null)
            {
                errors.Add(current.Message);
                current = current.InnerException;
            }

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al consultar registros de asistencia.",
                statusCode = 400,
                errors = errors
            });
        }
    }
}