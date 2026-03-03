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
/// - Crear registro de entrada/salida.
/// - Consultar registros por rango de fechas.
/// 
/// Seguridad:
/// - Requiere autenticación JWT.
/// - Aplica validaciones para evitar duplicados.
/// - Implementa soft-delete mediante IsDeleted.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AsistenciaController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección de dependencia del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public AsistenciaController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Identificador del origen del registro.
    /// 1 = Sistema (CatOrigen = S).
    /// </summary>
    private const int ORIGEN_SISTEMA = 1;

    // =========================================================
    // POST CreateRegistroAsistencia
    // =========================================================

    /// <summary>
    /// Crea un nuevo registro de asistencia (Entrada o Salida).
    ///
    /// Validaciones:
    /// - El empleado debe existir.
    /// - Si TipoRegistroId = 1 → HoraEntrada es obligatoria.
    /// - Si TipoRegistroId = 2 → HoraSalida es obligatoria.
    /// - No permite duplicados por:
    ///     (Empleado + Fecha + TipoRegistro + OrigenSistema).
    /// 
    /// El registro se guarda con:
    /// - DateCreated = UTC
    /// - IsDeleted = false
    /// - OrigenId = Sistema
    /// </summary>
    /// <param name="model">Datos para crear el registro.</param>
    /// <returns>
    /// ApiResponse con:
    /// - success = true si se crea correctamente.
    /// - AsistenciaId generado.
    /// </returns>
    [HttpPost("CreateRegistroAsistencia")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> CreateRegistroAsistencia(
        [FromBody] RegistroAsistenciaCreateRequest model)
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

            // Validar existencia del empleado
            var existeEmpleado = await _db.Empleados
                .AnyAsync(x => x.IdEmpleado == model.IdEmpleado);

            if (!existeEmpleado)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Empleado inválido.",
                    statusCode = 400
                });
            }

            var fecha = model.Fecha.Date;

            // Validaciones por tipo de registro
            if (model.TipoRegistroId == 1 && model.HoraEntrada == null)
                return BadRequest("HoraEntrada requerida.");

            if (model.TipoRegistroId == 2 && model.HoraSalida == null)
                return BadRequest("HoraSalida requerida.");

            // Validación anti-duplicado
            var existe = await _db.AsistenciaRegistros.AnyAsync(r =>
                r.IdEmpleado == model.IdEmpleado &&
                r.Fecha == fecha &&
                r.TipoRegistroId == model.TipoRegistroId &&
                r.OrigenId == ORIGEN_SISTEMA &&
                !r.IsDeleted);

            if (existe)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Ya existe registro.",
                    statusCode = 400
                });
            }

            var entity = new AsistenciaRegistroRequest
            {
                IdEmpleado = model.IdEmpleado,
                Fecha = fecha,
                TipoRegistroId = model.TipoRegistroId,
                HoraEntrada = model.TipoRegistroId == 1 ? model.HoraEntrada : null,
                HoraSalida = model.TipoRegistroId == 2 ? model.HoraSalida : null,
                Observaciones = model.Observaciones,
                OrigenId = ORIGEN_SISTEMA,
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.AsistenciaRegistros.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Registro creado.",
                data = new { entity.AsistenciaId },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = ex.Message,
                statusCode = 400
            });
        }
    }

    // =========================================================
    // GET GetRegistrosAsistencia
    // =========================================================

    /// <summary>
    /// Obtiene los registros de asistencia de un empleado
    /// dentro de un rango de fechas.
    ///
    /// Si no se envían fechas:
    /// - FechaInicio = Hoy - 7 días.
    /// - FechaFin = Hoy.
    ///
    /// La respuesta agrupa por fecha y devuelve:
    /// - HoraEntrada (TipoRegistroId = 1)
    /// - HoraSalida (TipoRegistroId = 2)
    ///
    /// Solo devuelve registros:
    /// - No eliminados (IsDeleted = false)
    /// - Origen = Sistema
    /// </summary>
    /// <param name="model">Filtros de consulta (IdEmpleado + rango fechas).</param>
    /// <returns>Listado agrupado por fecha.</returns>
    [HttpGet("GetRegistrosAsistencia")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetRegistrosAsistencia(
        [FromQuery] RegistroAsistenciaQuery model)
    {
        var requestId = Guid.NewGuid().ToString();

        var fi = (model.FechaInicio ?? DateTime.Today.AddDays(-7)).Date;
        var ff = (model.FechaFin ?? DateTime.Today).Date;

        var data = await _db.AsistenciaRegistros
            .Where(r =>
                r.IdEmpleado == model.IdEmpleado &&
                r.Fecha >= fi &&
                r.Fecha <= ff &&
                !r.IsDeleted &&
                r.OrigenId == ORIGEN_SISTEMA)
            .GroupBy(r => r.Fecha)
            .Select(g => new
            {
                Fecha = g.Key,
                HoraEntrada = g.Where(x => x.TipoRegistroId == 1)
                               .Select(x => x.HoraEntrada)
                               .FirstOrDefault(),
                HoraSalida = g.Where(x => x.TipoRegistroId == 2)
                              .Select(x => x.HoraSalida)
                              .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Consulta exitosa.",
            data = data,
            statusCode = 200
        });
    }
}