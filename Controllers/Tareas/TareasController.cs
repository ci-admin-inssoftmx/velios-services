using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Common;
using velios.Api.Models.Tareas;
using velios.Api.Models.Tareas.Requests;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TareasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TareasController> _logger;

    public TareasController(AppDbContext db, ILogger<TareasController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List()
    {
        var requestId = Guid.NewGuid().ToString();
        try
        {
            var data = await (
                from t in _db.Tareas.AsNoTracking()
                join c in _db.Clientes.AsNoTracking() on t.ClienteId equals c.ClienteId
                join e in _db.EstatusTareas.AsNoTracking() on t.EstatusTareaId equals e.EstatusTareaId
                where !t.IsDeleted && !c.IsDeleted
                orderby t.TareaId descending
                select new
                {
                    taskId = t.TaskCode,
                    title = t.Titulo,
                    description = t.Descripcion,
                    statusCode = e.Codigo,
                    client = new
                    {
                        name = c.RazonSocial ?? c.NombreComercial ?? "SIN NOMBRE",
                        logoUrl = (string?)null
                    },
                    schedule = new
                    {
                        assignedDate = t.FechaAsignacion,
                        programmedDate = t.FechaProgramada,
                        dueDate = t.FechaVencimiento
                    }
                }).ToListAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Consulta exitosa.",
                data = new { tasks = data },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar tareas.");
            return BadRequest(new ApiResponse<object> { request_id = requestId, success = false, message = "Error al consultar tareas.", statusCode = 400, errors = GetErrorMessages(ex) });
        }
    }

    [HttpGet("{taskId}")]
    public async Task<ActionResult<ApiResponse<object>>> Get(string taskId)
    {
        var requestId = Guid.NewGuid().ToString();
        try
        {
            var tarea = await (
                from t in _db.Tareas.AsNoTracking()
                join c in _db.Clientes.AsNoTracking() on t.ClienteId equals c.ClienteId
                join e in _db.EstatusTareas.AsNoTracking() on t.EstatusTareaId equals e.EstatusTareaId
                where !t.IsDeleted && !c.IsDeleted && t.TaskCode == taskId
                select new { Tarea = t, Cliente = c, Estatus = e }).FirstOrDefaultAsync();

            if (tarea == null)
            {
                return NotFound(new ApiResponse<object> { request_id = requestId, success = false, message = "Tarea no encontrada.", statusCode = 404 });
            }

            var observaciones = await _db.TareaObservaciones.AsNoTracking()
                .Where(x => x.TareaId == tarea.Tarea.TareaId)
                .OrderByDescending(x => x.ObservacionId)
                .Select(x => x.Observacion).ToListAsync();

            // CORRECCIÓN AQUÍ: Manejo de nulos en evidencias
            var evidencias = await _db.TareaEvidencias.AsNoTracking()
                .Where(x => x.TareaId == tarea.Tarea.TareaId)
                .OrderByDescending(x => x.EvidenciaId)
                .Select(x => new
                {
                    type = x.Tipo ?? "IMAGE",
                    url = x.UrlArchivo ?? "",
                    mimeType = x.MimeType ?? "image/png", // Evita el error "Data is Null"
                    sizeInBytes = x.SizeBytes ?? 0        // Evita el error "Data is Null"
                }).ToListAsync();

            var timeline = await (
                from tl in _db.TareaTimeline.AsNoTracking()
                join te in _db.TipoEventoTareas.AsNoTracking() on tl.TipoEventoTareaId equals te.TipoEventoTareaId
                where tl.TareaId == tarea.Tarea.TareaId
                orderby tl.TimelineId ascending
                select new
                {
                    eventId = $"EVT-{tl.TimelineId:D3}",
                    type = te.Codigo,
                    description = tl.Descripcion,
                    performedBy = tl.PerformedBy,
                    performedAt = tl.PerformedAt
                }).ToListAsync();

            return Ok(new
            {
                taskId = tarea.Tarea.TaskCode,
                title = tarea.Tarea.Titulo,
                description = tarea.Tarea.Descripcion,
                statusCode = tarea.Estatus.Codigo,
                client = new { name = tarea.Cliente.RazonSocial ?? tarea.Cliente.NombreComercial ?? "SIN NOMBRE" },
                observations = observaciones,
                evidences = evidencias,
                timeline = timeline,
                schedule = new { assignedDate = tarea.Tarea.FechaAsignacion, dueDate = tarea.Tarea.FechaVencimiento }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar detalle.");
            // Agregué GetErrorMessages para que puedas ver qué falla exactamente en el log si sigue el error
            return BadRequest(new ApiResponse<object> { request_id = requestId, success = false, message = "Error al consultar tarea.", statusCode = 400, errors = GetErrorMessages(ex) });
        }
    }

    [HttpPut("{taskId}")]
    public async Task<ActionResult<object>> Update(string taskId, [FromBody] TareaUpdateRequest model)
    {
        try
        {
            if (model == null) return BadRequest(new { success = false, message = "Solicitud inválida" });

            var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TaskCode == taskId && !x.IsDeleted);
            if (tarea == null) return NotFound(new { success = false, message = "Tarea no encontrada" });

            if (!string.IsNullOrWhiteSpace(model.NewStatusCode))
            {
                var nuevoEstatus = await _db.EstatusTareas.AsNoTracking().FirstOrDefaultAsync(x => x.Codigo == model.NewStatusCode);
                if (nuevoEstatus != null) tarea.EstatusTareaId = nuevoEstatus.EstatusTareaId;
            }

            if (!string.IsNullOrWhiteSpace(model.Observations))
            {
                _db.TareaObservaciones.Add(new TareaObservacion
                {
                    TareaId = tarea.TareaId,
                    Observacion = model.Observations.Trim(),
                    CreatedBy = User?.Identity?.Name ?? "SYSTEM",
                    DateCreated = DateTime.UtcNow
                });
            }

            if (model.EvidencePhotos != null && model.EvidencePhotos.Any())
            {
                foreach (var item in model.EvidencePhotos)
                {
                    _db.TareaEvidencias.Add(new TareaEvidencia
                    {
                        TareaId = tarea.TareaId,
                        Tipo = item.Type ?? "IMAGE",
                        UrlArchivo = item.Url,
                        DateCreated = DateTime.UtcNow,
                        Latitud = item.Location?.Latitude,
                        Longitud = item.Location?.Longitude,
                        Direccion = item.Address?.FormattedAddress,
                        Plataforma = item.DeviceInfo?.Platform,
                        ModeloDispositivo = item.DeviceInfo?.DeviceModel
                    });
                }
            }

            tarea.DateModified = model.UpdatedAt != default ? model.UpdatedAt : DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Actualizado correctamente", taskId = tarea.TaskCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Update.");
            return BadRequest(new { success = false, errors = GetErrorMessages(ex) });
        }
    }

    private static List<string> GetErrorMessages(Exception ex)
    {
        var errors = new List<string>();
        var current = ex;
        while (current != null) { errors.Add(current.Message); current = current.InnerException; }
        return errors;
    }
}