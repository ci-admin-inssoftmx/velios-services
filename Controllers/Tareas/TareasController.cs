using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Common;
using velios.Api.Models.Notificaciones;
using velios.Api.Models.Tareas;
using velios.Api.Models.Tareas.Requests;
using velios.Api.Utils;
using static QRCoder.PayloadGenerator;
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
    public async Task<ActionResult<ApiResponse<object>>> List([FromQuery] long idUsuario, [FromQuery] int tipoUsuario)
    {
        try
        {
            if (idUsuario <= 0 || (tipoUsuario != 1 && tipoUsuario != 2 && tipoUsuario != 3))
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "Parámetros inválidos.",
                    statusCode = 400,
                    errors = new List<string> { "idUsuario y tipoUsuario son obligatorios. tipoUsuario debe ser 1 (proveedor), 2 (supervisor) o 3 (operador)." }
                });
            }

            var query =
                from t in _db.Tareas.AsNoTracking()
                join c in _db.Clientes.AsNoTracking() on t.ClienteId equals c.ClienteId
                join e in _db.EstatusTareas.AsNoTracking() on t.EstatusTareaId equals e.EstatusTareaId
                join p in _db.ClienteProyectos.AsNoTracking()
                    on new { ProyectoId = t.ProyectoId, ClienteId = t.ClienteId }
                    equals new { ProyectoId = (int?)p.ProyectoId, ClienteId = p.ClienteId }
                    into pGroup
                from p in pGroup.DefaultIfEmpty()
                let ct = _db.CentrosTrabajo.AsNoTracking()
                     .Where(x => x.CentroTrabajoId == t.CentroTrabajoId && !x.IsDeleted)
                     .FirstOrDefault()
                join tr in _db.ProveedorTrabajadores.AsNoTracking() on t.TrabajadorId equals tr.TrabajadorId into trGroup
                from tr in trGroup.DefaultIfEmpty()
                join sv in _db.ProveedorTrabajadores.AsNoTracking() on t.SupervisorId equals sv.TrabajadorId into svGroup
                from sv in svGroup.DefaultIfEmpty()
                where !t.IsDeleted && !c.IsDeleted 
                select new { t, c, e, p, ct, tr, sv };

            // ── FILTRO POR USUARIO ──────────────────────────────────────────
            query = tipoUsuario == 1
                ? query.Where(x => x.t.ProveedorId == idUsuario)
                : tipoUsuario == 2
                    ? query.Where(x => x.t.SupervisorId == idUsuario)
                    : query.Where(x => x.t.TrabajadorId == idUsuario);
            // ─────────────────────────────────────────────────────────────

            var tareas = await query
                .OrderByDescending(x => x.t.TareaId)
                .Select(x => new
                {
                    tareaId = x.t.TareaId,
                    taskId = x.t.TaskCode,
                    title = x.t.Titulo,
                    description = x.t.Descripcion,
                    statusCode = x.e.Codigo,
                    planTrabajo = x.p != null ? x.p.Nombre : "SIN PLAN",
                    client = new
                    {
                        clienteId = x.t.ClienteId,
                        name = x.c.NombreComercial ?? x.c.RazonSocial ?? "SIN NOMBRE",
                        logoUrl = (string?)null
                    },
                    schedule = new
                    {
                        assignedDate = x.t.FechaAsignacion,
                        programmedDate = x.t.FechaProgramada,
                        dueDate = x.t.FechaVencimiento
                    },
                    trabajador = new
                    {
                        trabajadorId = x.tr == null ? (long?)null : x.tr.TrabajadorId,
                        nombre = x.tr == null ? null : $"{x.tr.Nombre} {x.tr.ApellidoPaterno}".Trim(),
                        tipoDeMiembro = x.tr == null ? null : x.tr.TipoDeMiembro
                    },
                    supervisor = new
                    {
                        supervisorId = x.sv == null ? (long?)null : x.sv.TrabajadorId,
                        nombre = x.sv == null ? null : $"{x.sv.Nombre} {x.sv.ApellidoPaterno}".Trim(),
                        tipoDeMiembro = x.sv == null ? null : x.sv.TipoDeMiembro
                    },
                    centroTrabajo = new
                    {
                        centroTrabajoId = x.ct == null ? (int?)null : x.ct.CentroTrabajoId,
                        nombre = x.ct == null ? null : x.ct.Nombre,
                        latitud = x.ct == null ? (decimal?)null : x.ct.Lat,
                        longitud = x.ct == null ? (decimal?)null : x.ct.Lng,
                        radioMetros = x.ct == null ? (int?)null : x.ct.RadioMetros,
                        zona = x.ct == null ? null : x.ct.Zona,
                        region = x.ct == null ? null : x.ct.Region,
                        calle = x.ct == null ? null : x.ct.Calle,
                        numero = x.ct == null ? null : x.ct.Numero,
                        colonia = x.ct == null ? null : x.ct.Colonia,
                        municipio = x.ct == null ? null : x.ct.Municipio,
                        codigoPostal = x.ct == null ? null : x.ct.CodigoPostal

                    },
                    presupuestoAsignado = x.t.PresupuestoAsignado,
                    presupuestoUsado = x.t.PresupuestoUsado,
                    presupuestoDisponible = x.t.PresupuestoDisponible,
                    presupuestoFinalAutorizado = x.t.PresupuestoFinalAutorizado
                }).ToListAsync();

            // ── GASTOS — se traen en una sola query con todos los TareaIds ──
            var tareaIds = tareas.Select(x => x.tareaId).ToList();



            // -------  Nueva lógica para historial de solicitudes de presupuesto excedido ----------
            // 
            // Se optienen los minutos que se toman en cuenta para determinar si el cliente no dio respuesta a una solicitud
            //Por default deben ser 1440 minitos = 24 horas
            var minutosSegundaNotificacion =
                await GetMinutosSegundaNotificacionAsync();

            
            // Se optiene el id del estatus del gasto con la clave SIN_RESPUESTA
            var idEstatusGastoSinRespuesta =
                await GetIdEstatusGastoSinRespuestaAsync(Constants.EstatusGasto.SIN_RESPUESTA);

            //determina la fecha limite de acuerdo a minutosSegundaNotificacion
            var fechaLimite = DateTime.Now.AddMinutes(
                -minutosSegundaNotificacion);


            var gastosPorTarea = await _db.GastosTarea.AsNoTracking()
                .Where(g => tareaIds.Contains(g.IdTarea))
                .OrderBy(g => g.IdGastoTarea)
                .Select(g => new
                {
                    g.IdTarea,
                    idGasto = g.IdGastoTarea,
                    gasto = g.Gasto,
                    fechaRegistro = g.FechaRegistro,
                    descripcion = g.Descripcion,        // ← NUEVO
                    registeredById = g.RegisteredById,     // ← NUEVO
                    registeredByType = g.RegisteredByType,   // ← NUEVO
                    nombreUsuario = g.RegisteredByType == "Proveedor"
                        ? _db.Proveedores
                            .Where(p => p.ProveedorId == g.RegisteredById)
                            .Select(p => p.NombreComercial)
                            .FirstOrDefault()
                        : g.RegisteredByType == "Trabajador"
                            ? _db.ProveedorTrabajadores
                                .Where(t => t.TrabajadorId == (long)g.RegisteredById)
                                .Select(t => (t.Nombre + " " + t.ApellidoPaterno + " " + t.ApellidoMaterno).Trim())
                                .FirstOrDefault()
                            : null,
                    execedePresupuesto = g.ExecedePresupuesto,
                    idCatEstatusGasto = g.IdCatEstatusGasto,
                    descripcionEstatus =
                        _db.CatEstatusGasto
                            .Where(p => g.IdCatEstatusGasto.HasValue &&
                                        p.IdCatEstatusGasto == g.IdCatEstatusGasto.Value)
                            .Select(p => p.Estatus)
                            .FirstOrDefault() ?? string.Empty,
                    fechaClienteRespuestaGasto = g.FechaClienteRespuestaGasto,
                    excedeTiempoRespuesta =
                        g.IdCatEstatusGasto == idEstatusGastoSinRespuesta &&
                        g.ExecedePresupuesto == true &&
                        g.FechaRegistro <= fechaLimite,
                    minutosSegundaNotificacion
                })
                .ToListAsync();
            // ───────────────────────────────────────────────────────────────

            // ── COMBINAR ────────────────────────────────────────────────────
            var data = tareas.Select(t => new
            {
                t.tareaId,
                t.taskId,
                t.title,
                t.description,
                t.statusCode,
                t.planTrabajo,
                t.client,
                t.schedule,
                t.trabajador,
                t.supervisor,
                t.centroTrabajo,
                presupuesto = new
                {
                    presupuestoAsignado = t.presupuestoAsignado,
                    presupuestoUsado = t.presupuestoUsado,
                    presupuestoDisponible = t.presupuestoDisponible,
                    presupuestoFinalAutorizado = t.presupuestoFinalAutorizado,
                    gastos = gastosPorTarea
                        .Where(g => g.IdTarea == t.tareaId)
                        .Select(g => new
                        {
                            g.idGasto,
                            g.gasto,
                            g.descripcion,
                            g.nombreUsuario,
                            g.fechaRegistro,
                            g.execedePresupuesto,
                            g.idCatEstatusGasto ,
                            g.descripcionEstatus,
                            g.fechaClienteRespuestaGasto,
                            g.excedeTiempoRespuesta,
                            g.minutosSegundaNotificacion

                        }).ToList()
                }
            }).ToList();
            // ───────────────────────────────────────────────────────────────

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

            // Se optienen los minutos que se toman en cuenta para determinar si el cliente no dio respuesta a una solicitud
            //Por default deben ser 1440 minitos = 24 horas
            var minutosSegundaNotificacion =
                await GetMinutosSegundaNotificacionAsync();

            // Se optiene el id del estatus del gasto con la clave SIN_RESPUESTA
            var idEstatusGastoSinRespuesta =
                await GetIdEstatusGastoSinRespuestaAsync(Constants.EstatusGasto.SIN_RESPUESTA);

            //determina la fecha limite de acuerdo a minutosSegundaNotificacion
            var fechaLimite = DateTime.Now.AddMinutes(
                -minutosSegundaNotificacion);




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

            // ── CENTRO DE TRABAJO ──────────────────────────────────────────────────
            var centroTrabajo = await _db.CentrosTrabajo.AsNoTracking()
                .Where(x => x.CentroTrabajoId == tarea.Tarea.CentroTrabajoId && !x.IsDeleted)
                .Select(x => new
                {
                    centroTrabajoId = x.CentroTrabajoId,
                    nombre = x.Nombre,
                    latitud = x.Lat,
                    longitud = x.Lng,
                    radioMetros = x.RadioMetros,
                    zona = x.Zona,
                    region = x.Region,
                    calle = x.Calle,
                    numero = x.Numero,
                    colonia = x.Colonia,
                    municipio = x.Municipio,
                    codigoPostal = x.CodigoPostal
                })
                .FirstOrDefaultAsync();
            // ──────────────────────────────────────────────────────────────────────

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
                    evidenceHash = x.EvidenceHash,
                    deviceInfo = new
                    {
                        platform = x.Plataforma,
                        appVersion = x.VersionApp,
                        deviceModel = x.ModeloDispositivo,
                        osVersion = x.VersionOS,
                        deviceUniqueId = x.DeviceUniqueId,
                        installationId = x.InstallationId,
                        deviceIdentifier = x.DeviceIdentifier,
                        isPhysicalDevice = x.IsPhysicalDevice
                    },
                    comentario = x.Comentario,
                    progreso = x.Progreso
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
                    previousValue = tl.ValorAnterior,
                    newValue = tl.ValorNuevo,
                    performedBy = tl.PerformedBy,
                    performedAt = tl.PerformedAt
                }).ToListAsync();
            // ── GASTOS ────────────────────────────────────────────────────────────
            var gastos = await _db.GastosTarea.AsNoTracking()
                .Where(x => x.IdTarea == tarea.Tarea.TareaId)
                .OrderBy(x => x.IdGastoTarea)
                .Select(x => new
                {
                    idGasto = x.IdGastoTarea,
                    gasto = x.Gasto,
                    fechaRegistro = x.FechaRegistro,
                    descripcion = x.Descripcion,        // ← NUEVO
                    registeredById = x.RegisteredById,     // ← NUEVO
                    registeredByType = x.RegisteredByType,   // ← NUEVO
                    nombreUsuario = x.RegisteredByType == "Proveedor"
                        ? _db.Proveedores
                            .Where(p => p.ProveedorId == x.RegisteredById)
                            .Select(p => p.NombreComercial)
                            .FirstOrDefault()
                        : x.RegisteredByType == "Trabajador"
                            ? _db.ProveedorTrabajadores
                                .Where(t => t.TrabajadorId == (long)x.RegisteredById)
                                .Select(t => (t.Nombre + " " + t.ApellidoPaterno + " " + t.ApellidoMaterno).Trim())
                                .FirstOrDefault()
                            : null,
                    execedePresupuesto = x.ExecedePresupuesto,
                    idCatEstatusGasto = x.IdCatEstatusGasto,
                    descripcionEstatus =
                        _db.CatEstatusGasto
                            .Where(p => x.IdCatEstatusGasto.HasValue &&
                                        p.IdCatEstatusGasto == x.IdCatEstatusGasto.Value)
                            .Select(p => p.Estatus)
                            .FirstOrDefault() ?? string.Empty,
                    fechaClienteRespuestaGasto = x.FechaClienteRespuestaGasto,
                    excedeTiempoRespuesta =
                        x.IdCatEstatusGasto == idEstatusGastoSinRespuesta &&
                        x.ExecedePresupuesto == true &&
                        x.FechaRegistro <= fechaLimite,
                    minutosSegundaNotificacion
                })
                .ToListAsync();
            // ─────────────────────────────────────────────────────────────────────
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
                        tipoDeMiembro = x.TipoDeMiembro,
                        telefono = x.Telefono
                    }).FirstOrDefaultAsync(),
                supervisor = tarea.Tarea.SupervisorId == null ? null : await _db.ProveedorTrabajadores.AsNoTracking()
                    .Where(x => x.TrabajadorId == tarea.Tarea.SupervisorId && !x.IsDeleted)
                    .Select(x => new
                    {
                        supervisorId = x.TrabajadorId,
                        nombre = $"{x.Nombre} {x.ApellidoPaterno}".Trim(),
                        tipoDeMiembro = x.TipoDeMiembro,
                        telefono = x.Telefono
                    }).FirstOrDefaultAsync(),
                observations = observaciones,
                evidences = evidencias,
                timeline = timeline,
                schedule = new
                {
                    assignedDate = tarea.Tarea.FechaAsignacion,
                    programmedDate = tarea.Tarea.FechaProgramada,
                    dueDate = tarea.Tarea.FechaVencimiento
                },
                centroTrabajo = centroTrabajo,

                // ── NUEVO ────────────────────────────────────────────────────────
                presupuesto = new
                {
                    presupuestoAsignado = tarea.Tarea.PresupuestoAsignado,
                    presupuestoUsado = tarea.Tarea.PresupuestoUsado,
                    presupuestoDisponible = tarea.Tarea.PresupuestoDisponible,
                    presupuestoFinalAutorizado = tarea.Tarea.PresupuestoFinalAutorizado,
                    gastos
                }
                // ─────────────────────────────────────────────────────────────────
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
                var createdBy = User?.Identity?.Name ?? "SYSTEM";
                _db.TareaObservaciones.Add(new TareaObservacion
                {
                    TareaId = tarea.TareaId,
                    Observacion = model.Observations.Trim(),
                    CreatedBy = createdBy.Length > 100 ? createdBy.Substring(0, 100) : createdBy,
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
                        EvidenceHash = item.EvidenceHash?.Trim(),           // ← NUEVO
                        DeviceUniqueId = item.DeviceInfo?.DeviceUniqueId,   // ← NUEVO
                        InstallationId = item.DeviceInfo?.InstallationId,   // ← NUEVO
                        DeviceIdentifier = item.DeviceInfo?.DeviceIdentifier, // ← NUEVO
                        IsPhysicalDevice = item.DeviceInfo?.IsPhysicalDevice, // ← NUEVO
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
                        Descripcion = item.Description?.Length > 500
                                              ? item.Description.Substring(0, 500)
                                              : item.Description,
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


    /// <summary>
    /// Obtiene los mensajes de error de una excepción y de todas sus excepciones
    /// internas asociadas.
    /// </summary>
    /// <param name="ex">
    /// Excepción de la que se desean obtener los mensajes de error.
    /// </param>
    /// <returns>
    /// Lista de mensajes correspondientes a la excepción principal y a cada una
    /// de sus excepciones internas, en el orden en que se encuentran anidadas.
    /// </returns>
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

    /// <summary>
    /// Obtiene la cantidad de minutos configurada para determinar el tiempo de espera
    /// antes de enviar una segunda notificación.
    /// </summary>
    /// <returns>
    /// Cantidad de minutos configurada para la segunda notificación.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando la configuración no existe o su valor no es un número entero
    /// válido mayor que cero.
    /// </exception>
    private async Task<int> GetMinutosSegundaNotificacionAsync()
    {
        var configuracion = await _db.ConfiguracionNotificacion
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Clave == Constants.ConfiguracionNotificacion
                    .TIEMPO_SEGUNDA_NOTIFICACION_MINUTOS);

        return GetConfigInt(configuracion?.Valor);
    }

    /// <summary>
    /// Convierte el valor de una configuración a un número entero válido.
    /// </summary>
    /// <param name="valor">
    /// Valor de configuración que se desea convertir.
    /// </param>
    /// <returns>
    /// Número entero obtenido a partir del valor proporcionado.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando el valor no es un número entero válido o es menor o igual a cero.
    /// </exception>
    private int GetConfigInt(string? valor)
    {
        if (!int.TryParse(valor, out var resultado) || resultado <= 0)
        {
            throw new InvalidOperationException(
                $"La configuración '{valor}' debe ser un número entero mayor a cero.");
        }

        return resultado;
    }


    /// <summary>
    /// Obtiene el identificador del estatus de gasto correspondiente a la descripción
    /// proporcionada.
    /// </summary>
    /// <param name="estatusDesc">
    /// Descripción del estatus de gasto que se desea consultar.
    /// </param>
    /// <returns>
    /// Identificador del estatus de gasto encontrado.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando no existe un estatus de gasto con la descripción proporcionada.
    /// </exception>
    private async Task<int> GetIdEstatusGastoSinRespuestaAsync(string estatusDesc)
    {
        var estatus = await _db.CatEstatusGasto
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Estatus == estatusDesc);

        if (estatus == null)
        {
            throw new InvalidOperationException(
                $"No se encontró el estatus de gasto '{estatusDesc}'.");
        }

        return estatus.IdCatEstatusGasto;
    }
}