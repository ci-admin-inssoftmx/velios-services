using Microsoft.AspNetCore.Mvc;
using velios.Api.Services;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReporteMaterialidadController : ControllerBase
{
    private readonly IReporteMaterialidadService _reporteMaterialidadService;

    public ReporteMaterialidadController(IReporteMaterialidadService reporteMaterialidadService)
    {
        _reporteMaterialidadService = reporteMaterialidadService;
    }

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
    [HttpPost("tarea/{tareaId}/iniciar")]
    public IActionResult IniciarGeneracion(int tareaId, [FromServices] ProgresoStore progresoStore)
    {
        var jobId = progresoStore.Crear();

        // Fire-and-forget: corre en background, el request regresa el jobId al toque.
        _ = Task.Run(async () =>
        {
            try
            {
                await _reporteMaterialidadService.GenerarPdfPorTareaAsync(tareaId, jobId, progresoStore);
            }
            catch
            {
                // El servicio ya deja Estado="Error" en el store; aquí solo evitamos
                // que una excepción no observada tumbe el proceso.
            }
        });

        return Ok(new { jobId });
    }

    [HttpGet("progreso/{jobId}")]
    public IActionResult ConsultarProgreso(Guid jobId, [FromServices] ProgresoStore progresoStore)
    {
        var progreso = progresoStore.Obtener(jobId);
        if (progreso is null) return NotFound(new { mensaje = "Job no encontrado o expiró." });

        return Ok(new
        {
            estado = progreso.Estado,
            porcentaje = progreso.Porcentaje,
            procesadas = progreso.Procesadas,
            total = progreso.Total,
            mensaje = progreso.Mensaje
        });
    }

    [HttpGet("descargar/{jobId}")]
    public IActionResult Descargar(Guid jobId, [FromServices] ProgresoStore progresoStore)
    {
        var progreso = progresoStore.Obtener(jobId);
        if (progreso?.PdfBytes is null) return NotFound(new { mensaje = "El PDF aún no está listo o el job expiró." });

        return File(progreso.PdfBytes, "application/pdf", $"reporte-materialidad-{jobId}.pdf");
    }
}