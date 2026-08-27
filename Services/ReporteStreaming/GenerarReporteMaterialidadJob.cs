using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using velios.Api.Models; // ajustar si ReporteCacheEntity/Estados quedaron en otro namespace
using velios.Api.Data;

namespace velios.Api.Services
{
    /// <summary>
    /// Job de Hangfire: genera el PDF de materialidad de UNA tarea fuera del request HTTP,
    /// reportando avance evidencia por evidencia (vía el overload con IProgress agregado
    /// a ReporteMaterialidadService), y al terminar sube el archivo y actualiza el cache.
    /// </summary>
    public class GenerarReporteMaterialidadJob
    {
        private readonly IReporteMaterialidadService _reporteMaterialidadService;
        private readonly IProgresoStore _progresoStore;
        private readonly IReporteCacheRepository _reporteCacheRepository;
        private readonly IAlmacenamientoService _almacenamientoService;

        public GenerarReporteMaterialidadJob(
            IReporteMaterialidadService reporteMaterialidadService,
            IProgresoStore progresoStore,
            IReporteCacheRepository reporteCacheRepository,
            IAlmacenamientoService almacenamientoService)
        {
            _reporteMaterialidadService = reporteMaterialidadService;
            _progresoStore = progresoStore;
            _reporteCacheRepository = reporteCacheRepository;
            _almacenamientoService = almacenamientoService;
        }

        // Firma pensada para BackgroundJob.Enqueue<GenerarReporteMaterialidadJob>(j => j.Ejecutar(jobId, claveHash, tareaId));
        public async Task Ejecutar(string jobId, string claveHash, int tareaId)
        {
            try
            {
                var progreso = new Progress<(int Procesados, int Total)>(p =>
                {
                    _progresoStore.ActualizarTotal(jobId, p.Total);
                    _progresoStore.ActualizarProgreso(jobId, p.Procesados);
                    // Nota: no actualizamos tb_ReporteCache en cada tick (sería demasiado I/O
                    // a BD por cada evidencia); el progreso fino vive solo en ProgresoStore
                    // (memoria), que es lo que consulta el polling del frontend.
                });

                var pdfBytes = await _reporteMaterialidadService.GenerarPdfPorTareaAsync(tareaId, progreso);

                var nombreArchivo = $"Materialidad_Tarea{tareaId}_{claveHash.Substring(0, 12)}.pdf";

                await using var ms = new MemoryStream(pdfBytes);
                var url = await _almacenamientoService.Guardar(ms, nombreArchivo);

                await _reporteCacheRepository.MarcarCompletado(
                    claveHash,
                    urlDescarga: url,
                    rutaAlmacenamiento: url,
                    fechaExpiracion: DateTime.UtcNow.AddHours(6)); // TTL corto: las evidencias de una tarea pueden seguir creciendo

                _progresoStore.MarcarCompletado(jobId, url);
            }
            catch (Exception ex)
            {
                _progresoStore.MarcarError(jobId, ex.Message);
                await _reporteCacheRepository.MarcarError(claveHash, ex.Message);
                throw; // Hangfire registra el fallo en su dashboard y puede reintentar según configuración
            }
        }
    }
}