using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Asistencia;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AsistenciaController : ControllerBase
{
    private readonly AppDbContext _db;

    public AsistenciaController(AppDbContext db)
    {
        _db = db;
    }

    private const int ORIGEN_SISTEMA = 1; // CatOrigen = S

    // =========================
    // POST CreateRegistroAsistencia
    // =========================
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

            // Validar empleado
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

            // Validar tipo registro
            if (model.TipoRegistroId == 1 && model.HoraEntrada == null)
                return BadRequest("HoraEntrada requerida.");

            if (model.TipoRegistroId == 2 && model.HoraSalida == null)
                return BadRequest("HoraSalida requerida.");

            // Anti duplicado
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

    // =========================
    // GET Registros
    // =========================
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