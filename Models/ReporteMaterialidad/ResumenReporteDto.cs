namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// DTO con métricas globales del reporte.
/// </summary>
public class ResumenReporteDto
{
    public int TotalTareas { get; set; }
    public int TotalEvidencias { get; set; }
    public int TotalEvidenciasConGeo { get; set; }
    public int TotalEvidenciasSinGeo { get; set; }
    public DateTime? PrimeraEvidencia { get; set; }
    public DateTime? UltimaEvidencia { get; set; }
}