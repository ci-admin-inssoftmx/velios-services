using velios.Api.Models.ReporteMaterialidad;

namespace velios.Api.Services;

/// <summary>
/// Contrato para obtener la información necesaria
/// para armar el reporte de materialidad por tarea.
/// </summary>
public interface IReporteMaterialidadRepository
{
    Task<TareaReporteDto?> ObtenerTareaAsync(int tareaId);
    Task<ClienteReporteDto?> ObtenerClienteAsync(int clienteId);
    Task<List<EvidenciaReporteDto>> ObtenerEvidenciasPorTareaAsync(int tareaId);
}