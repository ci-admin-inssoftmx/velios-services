using velios.Api.Models.ReporteMaterialidad;

namespace velios.Api.Services;

/// <summary>
/// Contrato para obtener la información necesaria
/// para armar el reporte de materialidad por tarea.
/// </summary>
public interface IReporteMaterialidadRepository
{
    Task<TareaReporteDto?> ObtenerTareaAsync(int tareaId);
    Task<List<string>> ObtenerObservacionesPorTareaAsync(int tareaId);
    Task<string?> ObtenerDireccionCentroTrabajoAsync(int? centroTrabajoId);
    Task<string?> ObtenerTelefonoCentroTrabajoAsync(int? centroTrabajoId);
    Task<string?> ObtenerNombreCentroTrabajoAsync(int? centroTrabajoId); // NUEVO


    Task<ClienteReporteDto?> ObtenerClienteAsync(int clienteId);
    Task<List<EvidenciaReporteDto>> ObtenerEvidenciasPorTareaAsync(int tareaId);
}