using Hangfire;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using velios.Api.Data;
using velios.Api.Models;
using velios.Api.Services;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReporteMaterialidadController : ControllerBase
{
    private readonly IReporteMaterialidadService _reporteMaterialidadService;
    private readonly IReporteCacheRepository _reporteCacheRepository;
    private readonly IProgresoStore _progresoStore;

    public ReporteMaterialidadController(
        IReporteMaterialidadService reporteMaterialidadService,
        IReporteCacheRepository reporteCacheRepository,
        IProgresoStore progresoStore)
    {
        _reporteMaterialidadService = reporteMaterialidadService;
        _reporteCacheRepository = reporteCacheRepository;
        _progresoStore = progresoStore;
    }

    // -----------------------------------------------------------------
    // ENDPOINT EXISTENTE — se conserva tal cual.
    // -----------------------------------------------------------------
    [HttpGet("tarea/{tareaId}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> GenerarPorTarea(int tareaId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var pdfBytes = await _reporteMaterialidadService.GenerarPdfPorTareaAsync(tareaId);

        stopwatch.Stop();

        Response.Headers["X-Tiempo-Generacion"] = $"{stopwatch.ElapsedMilliseconds} ms";
        Response.Headers["Content-Length"] = pdfBytes.Length.ToString();

        return File(
            pdfBytes,
            "application/pdf",
            $"reporte-materialidad-{tareaId}.pdf"
        );
    }

    // -----------------------------------------------------------------
    // NUEVO — flujo async con cache + progreso.
    // Fix aplicado: maneja el caso de un reporte Completado ya expirado
    // (lo pasa a Expirado antes de crear uno nuevo, en vez de tronar
    // contra el índice único), y protege contra la carrera de dos
    // solicitudes casi simultáneas para el mismo hash.
    // -----------------------------------------------------------------
    [HttpPost("tarea/{tareaId}/solicitar")]
    public async Task<IActionResult> Solicitar(int tareaId, [FromQuery] int usuarioId)
    {
        var claveHash = ReporteCacheKeyBuilder.Calcular("MaterialidadTarea", new { tareaId }, proveedorId: null, clienteId: null);

        var existente = await _reporteCacheRepository.ObtenerVigentePorClave(claveHash);

        if (existente != null && existente.Estado == ReporteCacheEstados.Completado)
        {
            var vigente = existente.FechaExpiracion == null || existente.FechaExpiracion > DateTime.UtcNow;
            if (vigente)
            {
                return Ok(new { estado = "listo", url = existente.UrlDescarga });
            }

            // Ya expiró pero el recurring job de limpieza todavía no lo marcó
            // (ventana de hasta 1h). Lo sacamos del filtro del índice único
            // antes de crear uno nuevo, para no chocar con él.
            await _reporteCacheRepository.MarcarExpirado(claveHash);
            existente = null;
        }

        if (existente != null && existente.Estado == ReporteCacheEstados.Procesando)
        {
            return Ok(new { estado = "procesando", jobId = existente.HangfireJobId });
        }

        var jobId = Guid.NewGuid().ToString("N");

        try
        {
            await _reporteCacheRepository.Crear(new ReporteCacheEntity
            {
                ClaveHash = claveHash,
                TipoReporte = "MaterialidadTarea",
                FiltrosJson = JsonSerializer.Serialize(new { tareaId }),
                UsuarioSolicitanteId = usuarioId,
                Estado = ReporteCacheEstados.Procesando,
                HangfireJobId = jobId,
                FechaSolicitud = DateTime.UtcNow
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            // Dos solicitudes casi simultáneas para el mismo hash: la otra
            // ganó la carrera y ya insertó su fila. En vez de devolver un
            // 500, respondemos con el estado real de esa otra solicitud.
            var otraVigente = await _reporteCacheRepository.ObtenerVigentePorClave(claveHash);
            if (otraVigente != null)
            {
                return otraVigente.Estado == ReporteCacheEstados.Completado
                    ? Ok(new { estado = "listo", url = otraVigente.UrlDescarga })
                    : Ok(new { estado = "procesando", jobId = otraVigente.HangfireJobId });
            }
            throw; // caso raro: no encontramos nada, no ocultamos el error real
        }

        _progresoStore.Iniciar(jobId);

        BackgroundJob.Enqueue<GenerarReporteMaterialidadJob>(job => job.Ejecutar(jobId, claveHash, tareaId));

        return Ok(new { estado = "procesando", jobId });
    }

    // -----------------------------------------------------------------
    // NUEVO — polling del frontend cada 1.5-2s mientras estado == "procesando".
    // -----------------------------------------------------------------
    [HttpGet("progreso/{jobId}")]
    public IActionResult Progreso(string jobId)
    {
        var progreso = _progresoStore.Obtener(jobId);
        if (progreso == null) return NotFound();

        return Ok(new
        {
            estado = progreso.Estado,
            procesados = progreso.RegistrosProcesados,
            total = progreso.TotalRegistros,
            porcentaje = progreso.PorcentajeCompletado,
            urlDescarga = progreso.UrlDescarga,
            error = progreso.MensajeError
        });
    }

    [HttpGet("test-almacenamiento")]
    public async Task<IActionResult> TestAlmacenamiento([FromServices] IAlmacenamientoService almacenamiento)
    {
        var contenido = System.Text.Encoding.UTF8.GetBytes("prueba de guardado");
        using var stream = new MemoryStream(contenido);
        var url = await almacenamiento.Guardar(stream, "prueba.txt");
        return Ok(new { url });
    }

    [HttpGet("test-hash")]
    public IActionResult TestHash()
    {
        var hash1 = ReporteCacheKeyBuilder.Calcular("MaterialidadTarea", new { tareaId = 2676 }, proveedorId: null, clienteId: null);
        var hash2 = ReporteCacheKeyBuilder.Calcular("MaterialidadTarea", new { tareaId = 2676 }, proveedorId: null, clienteId: null);
        var hash3 = ReporteCacheKeyBuilder.Calcular("MaterialidadTarea", new { tareaId = 9999 }, proveedorId: null, clienteId: null);

        return Ok(new
        {
            hash1,
            hash2,
            hash3,
            mismosFiltrosCoinciden = hash1 == hash2,
            filtroDistintoCambia = hash1 != hash3
        });
    }
}