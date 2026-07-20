namespace velios.Api.Services;

/// <summary>
/// Contrato del servicio que arma y genera
/// el PDF de reporte de materialidad preeliminar por tarea.
/// </summary>
public interface IReporteMaterialidadPreeliminarService
{
    Task<byte[]> GenerarPdfPorTareaAsync(int tareaId);
}