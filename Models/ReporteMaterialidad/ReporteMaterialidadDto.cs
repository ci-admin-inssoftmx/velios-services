namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// DTO raíz del reporte completo por tarea.
/// </summary>
public class ReporteMaterialidadDto
{
    public ClienteReporteDto Cliente { get; set; } = new();
    public TareaReporteDto Tarea { get; set; } = new();
    public ResumenReporteDto Resumen { get; set; } = new();
    public DateTime FechaGeneracion { get; set; } = DateTime.Now;
}