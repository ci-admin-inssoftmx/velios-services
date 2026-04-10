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

    [HttpGet("tarea/{tareaId:int}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> GenerarPorTarea(int tareaId)
    {
        var pdfBytes = await _reporteMaterialidadService.GenerarPdfPorTareaAsync(tareaId);

        return File(
            pdfBytes,
            "application/pdf",
            $"InformeMaterialidad_Tarea_{tareaId}.pdf");
    }
}