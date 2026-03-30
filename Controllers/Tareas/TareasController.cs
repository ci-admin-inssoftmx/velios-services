using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Common;
using velios.Api.Models.Tareas;
using velios.Api.Models.Tareas.Requests;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado del módulo de tareas.
///
/// Endpoints:
/// - GET /api/tasks
/// - GET /api/tasks/{taskId}
/// - PUT /api/tasks/{taskId}
/// </summary>
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

    /// <summary>
    /// Obtiene el listado de tareas.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List()
    {
        

        try
        {
            var data = await (
    from t in _db.Tareas.AsNoTracking()
    join c in _db.Clientes.AsNoTracking() on t.ClienteId equals c.ClienteId
    join e in _db.EstatusTareas.AsNoTracking() on t.EstatusTareaId equals e.EstatusTareaId
    join tr in _db.ProveedorTrabajadores.AsNoTracking() on t.TrabajadorId equals tr.TrabajadorId into trGroup
    from tr in trGroup.DefaultIfEmpty()
    join sv in _db.ProveedorTrabajadores.AsNoTracking() on t.SupervisorId equals sv.TrabajadorId into svGroup
    from sv in svGroup.DefaultIfEmpty()
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
        },
        trabajador = new
        {
            trabajadorId = tr == null ? (long?)null : tr.TrabajadorId,
            nombre = tr == null ? null : $"{tr.Nombre} {tr.ApellidoPaterno}".Trim(),
            tipoDeMiembro = tr == null ? null : tr.TipoDeMiembro
        },
        supervisor = new
        {
            supervisorId = sv == null ? (long?)null : sv.TrabajadorId,
            nombre = sv == null ? null : $"{sv.Nombre} {sv.ApellidoPaterno}".Trim(),
            tipoDeMiembro = sv == null ? null : sv.TipoDeMiembro
        }
    }).ToListAsync();


            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Consulta exitosa.",
                data = new { tasks = data },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar tareas.");

            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al consultar tareas.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    /// <summary>
    /// Obtiene el detalle de una tarea por TaskCode.
    /// </summary>
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
                .Select(x => x.Observacion)
                .ToListAsync();

            var evidencias = await _db.TareaEvidencias.AsNoTracking()
                .Where(x => x.TareaId == tarea.Tarea.TareaId)
                .OrderByDescending(x => x.EvidenciaId)
                .Select(x => new
                {
                    type = x.Tipo ?? "IMAGE",
                    url = x.UrlArchivo ?? "",
                    mimeType = x.MimeType ?? null,
                    sizeInBytes = x.SizeBytes ?? null,
                    createdAt = x.DateCreated,
                    location = new
                    {
                        latitude = x.Latitud,
                        longitude = x.Longitud,
                        accuracyMeters = x.PrecisionMetros,
                        altitude = x.Altitud,
                        heading = x.DireccionGrados,
                        speed = x.Velocidad,
                        speedAccuracy = x.PrecisionVelocidad,
                        timestamp = x.TimestampGps,
                        isMocked = x.EsSimulado
                    },
                    address = new
                    {
                        formattedAddress = x.Direccion
                    },
                    deviceInfo = new
                    {
                        platform = x.Plataforma,
                        appVersion = x.VersionApp,
                        deviceModel = x.ModeloDispositivo,
                        osVersion = x.VersionOS
                    },
                    // --- NUEVOS CAMPOS ---
                    comentario = x.Comentario,
                    progreso = x.Progreso
                    // ---------------------
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
                    // Complemento: Valores de cambio si existen en tu tabla timeline
                    previousValue = tl.ValorAnterior,
                    newValue = tl.ValorNuevo,
                    performedBy = tl.PerformedBy,
                    performedAt = tl.PerformedAt
                }).ToListAsync();

            return Ok(new
            {
                taskId = tarea.Tarea.TaskCode,
                title = tarea.Tarea.Titulo,
                description = tarea.Tarea.Descripcion,
                statusCode = tarea.Estatus.Codigo,
                createdAt = tarea.Tarea.DateCreated,
                updatedAt = tarea.Tarea.DateModified,
                client = new { name = tarea.Cliente.RazonSocial ?? tarea.Cliente.NombreComercial ?? "SIN NOMBRE" },
                trabajador = tarea.Tarea.TrabajadorId == null ? null : await _db.ProveedorTrabajadores.AsNoTracking()
         .Where(x => x.TrabajadorId == tarea.Tarea.TrabajadorId && !x.IsDeleted)
         .Select(x => new
         {
             trabajadorId = x.TrabajadorId,
             nombre = $"{x.Nombre} {x.ApellidoPaterno}".Trim(),
             tipoDeMiembro = x.TipoDeMiembro
         }).FirstOrDefaultAsync(),
                supervisor = tarea.Tarea.SupervisorId == null ? null : await _db.ProveedorTrabajadores.AsNoTracking()
         .Where(x => x.TrabajadorId == tarea.Tarea.SupervisorId && !x.IsDeleted)
         .Select(x => new
         {
             supervisorId = x.TrabajadorId,
             nombre = $"{x.Nombre} {x.ApellidoPaterno}".Trim(),
             tipoDeMiembro = x.TipoDeMiembro
         }).FirstOrDefaultAsync(),
                observations = observaciones,
                evidences = evidencias,
                timeline = timeline,
                schedule = new
                {
                    assignedDate = tarea.Tarea.FechaAsignacion,
                    programmedDate = tarea.Tarea.FechaProgramada,
                    dueDate = tarea.Tarea.FechaVencimiento
                }
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar detalle.");
            return BadRequest(new ApiResponse<object> { request_id = requestId, success = false, message = "Error al consultar tarea.", statusCode = 400, errors = GetErrorMessages(ex) });
        }
    }
    /// <summary>
    /// Actualiza una tarea agregando evidencias, observación y/o cambio de estatus.
    /// </summary>
    [HttpPut("{taskId}")]
    public async Task<ActionResult<object>> Update(string taskId, [FromBody] TareaUpdateRequest model)
    {
        try
        {
            if (model == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Solicitud inválida",
                    errors = new[]
                    {
                        new { field = "body", message = "El cuerpo de la solicitud es obligatorio." }
                    }
                });
            }

            var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TaskCode == taskId && !x.IsDeleted);

            if (tarea == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Tarea no encontrada"
                });
            }

            var tieneEvidencias = model.EvidencePhotos != null && model.EvidencePhotos.Any();
            var tieneObservacion = !string.IsNullOrWhiteSpace(model.Observations);
            var tieneNuevoEstado = !string.IsNullOrWhiteSpace(model.NewStatusCode);

            if (!tieneEvidencias && !tieneObservacion && !tieneNuevoEstado)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Solicitud inválida",
                    errors = new[]
                    {
                        new
                        {
                            field = "request",
                            message = "Debe enviar al menos uno de: evidencePhotos, observations o newStatusCode."
                        }
                    }
                });
            }

            if (model.UpdatedAt == default)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Solicitud inválida",
                    errors = new[]
                    {
                        new
                        {
                            field = "updatedAt",
                            message = "updatedAt es obligatorio."
                        }
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(model.NewStatusCode))
            {
                var nuevoEstatus = await _db.EstatusTareas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Codigo == model.NewStatusCode);

                if (nuevoEstatus == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "statusCode inválido",
                        errors = new[]
                        {
                            new
                            {
                                field = "newStatusCode",
                                message = $"El código '{model.NewStatusCode}' no existe en el catálogo"
                            }
                        }
                    });
                }

                tarea.EstatusTareaId = nuevoEstatus.EstatusTareaId;
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
                        Tipo = item.Type?.Trim() ?? "IMAGE",
                        UrlArchivo = item.Url,
                        MimeType = null,
                        SizeBytes = null,
                        Latitud = item.Location?.Latitude,
                        Longitud = item.Location?.Longitude,
                        PrecisionMetros = item.Location?.AccuracyMeters,
                        Altitud = item.Location?.Altitude,
                        DireccionGrados = item.Location?.Heading,
                        Velocidad = item.Location?.Speed,
                        PrecisionVelocidad = item.Location?.SpeedAccuracy,
                        TimestampGps = item.Location?.Timestamp,
                        EsSimulado = item.Location?.IsMocked,
                        Direccion = item.Address?.FormattedAddress,
                        Plataforma = item.DeviceInfo?.Platform,
                        VersionApp = item.DeviceInfo?.AppVersion,
                        ModeloDispositivo = item.DeviceInfo?.DeviceModel,
                        VersionOS = item.DeviceInfo?.OsVersion,
                        // --- NUEVOS CAMPOS ---
                        Comentario = item.Comentario?.Trim(),
                        Progreso = item.Progreso,
                        // ---------------------
                        DateCreated = DateTime.UtcNow
                    });
                }
            }

            if (model.TimelineEvents != null && model.TimelineEvents.Any())
            {
                foreach (var item in model.TimelineEvents)
                {
                    var tipoEvento = await _db.TipoEventoTareas
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Codigo == item.Type);

                    if (tipoEvento == null)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "timelineType inválido",
                            errors = new[]
                            {
                                new
                                {
                                    field = "timelineEvents.type",
                                    message = $"El código '{item.Type}' no existe en el catálogo"
                                }
                            }
                        });
                    }

                    _db.TareaTimeline.Add(new TareaTimeline
                    {
                        TareaId = tarea.TareaId,
                        TipoEventoTareaId = tipoEvento.TipoEventoTareaId,
                        Descripcion = item.Description,
                        ValorAnterior = item.PreviousValue,
                        ValorNuevo = item.NewValue,
                        PerformedBy = item.PerformedBy,
                        PerformedAt = item.PerformedAt,
                        DateCreated = DateTime.UtcNow,
                    });
                }
            }

            tarea.DateModified = model.UpdatedAt;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Tarea actualizada correctamente",
                taskId = tarea.TaskCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar tarea {TaskId}.", taskId);

            return BadRequest(new
            {
                success = false,
                message = "Ocurrió un error al actualizar la tarea",
                errors = GetErrorMessages(ex)
            });
        }
    }

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