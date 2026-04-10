using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.ReporteMaterialidad;

namespace velios.Api.Services;

/// <summary>
/// Implementación del repositorio de reportes de materialidad
/// usando AppDbContext sin afectar la lógica existente.
/// </summary>
public class ReporteMaterialidadRepository : IReporteMaterialidadRepository
{
    private readonly AppDbContext _context;

    public ReporteMaterialidadRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene una tarea específica con nombres de operador y supervisor.
    /// </summary>
    public async Task<TareaReporteDto?> ObtenerTareaAsync(int tareaId)
    {
        var query =
            from t in _context.Tareas.AsNoTracking()
            join et in _context.EstatusTareas.AsNoTracking()
                on t.EstatusTareaId equals et.EstatusTareaId

            join opTmp in _context.ProveedorTrabajadores.AsNoTracking()
                on t.TrabajadorId equals opTmp.TrabajadorId into operadoresJoin
            from operador in operadoresJoin.DefaultIfEmpty()

            join supTmp in _context.ProveedorTrabajadores.AsNoTracking()
                on t.SupervisorId equals supTmp.TrabajadorId into supervisoresJoin
            from supervisor in supervisoresJoin.DefaultIfEmpty()

            where t.TareaId == tareaId && !t.IsDeleted
            select new TareaReporteDto
            {
                TareaId = t.TareaId,
                TaskCode = t.TaskCode,
                ClienteId = t.ClienteId,
                ProyectoId = t.ProyectoId,
                Titulo = t.Titulo,
                Descripcion = t.Descripcion,

                EstatusTareaId = t.EstatusTareaId,
                EstatusCodigo = et.Codigo,
                EstatusNombre = et.Nombre,

                TrabajadorId = t.TrabajadorId,
                SupervisorId = t.SupervisorId,

                NombreOperador = operador != null
                    ? ((operador.Nombre ?? "") + " " + (operador.ApellidoPaterno ?? "") + " " + (operador.ApellidoMaterno ?? "")).Trim()
                    : null,

                NombreSupervisor = supervisor != null
                    ? ((supervisor.Nombre ?? "") + " " + (supervisor.ApellidoPaterno ?? "") + " " + (supervisor.ApellidoMaterno ?? "")).Trim()
                    : null,

                FechaAsignacion = t.FechaAsignacion,
                FechaProgramada = t.FechaProgramada,
                FechaVencimiento = t.FechaVencimiento,

                PresupuestoAsignado = t.PresupuestoAsignado,
                Moneda = t.Moneda,

                DateCreated = t.DateCreated,
                DateModified = t.DateModified,

                IsDeleted = t.IsDeleted,
                ImageURL = t.ImagenUrl,
                CentroTrabajoId = t.CentroTrabajoId
            };

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Obtiene la información principal del cliente.
    /// </summary>
    public async Task<ClienteReporteDto?> ObtenerClienteAsync(int clienteId)
    {
        return await _context.Clientes
            .AsNoTracking()
            .Where(c => c.ClienteId == clienteId)
            .Select(c => new ClienteReporteDto
            {
                ClienteId = c.ClienteId,
                //Nombre = c.Nombre,
                RazonSocial = c.RazonSocial,
                //Direccion = c.Direccion,
                Telefono = c.TelefonoContacto,
                Email = c.CorreoContacto,
                RFC = c.RFC
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Obtiene las evidencias asociadas a una tarea.
    /// </summary>
    public async Task<List<EvidenciaReporteDto>> ObtenerEvidenciasPorTareaAsync(int tareaId)
    {
        return await _context.TareaEvidencias
            .AsNoTracking()
            .Where(e => e.TareaId == tareaId)
            .OrderBy(e => e.DateCreated)
            .ThenBy(e => e.EvidenciaId)
            .Select(e => new EvidenciaReporteDto
            {
                EvidenciaId = e.EvidenciaId,
                TareaId = e.TareaId,
                Tipo = e.Tipo,
                UrlArchivo = e.UrlArchivo,
                MimeType = e.MimeType,
                SizeBytes = e.SizeBytes,

                Latitud = e.Latitud,
                Longitud = e.Longitud,
                Direccion = e.Direccion,

                Plataforma = e.Plataforma,
                VersionApp = e.VersionApp,
                ModeloDispositivo = e.ModeloDispositivo,
                VersionOS = e.VersionOS,

                DateCreated = e.DateCreated,
                PrecisionMetros = e.PrecisionMetros,
                Altitud = e.Altitud,
                DireccionGrados = e.DireccionGrados,
                Velocidad = e.Velocidad,
                PrecisionVelocidad = e.PrecisionVelocidad,
                TimestampGps = e.TimestampGps,

                EsSimulado = e.EsSimulado,
                Comentario = e.Comentario,
                Progreso = e.Progreso
            })
            .ToListAsync();
    }
}