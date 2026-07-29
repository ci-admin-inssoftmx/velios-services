namespace velios.Api.Services;

/// <summary>
/// Contrato del servicio que arma y genera
/// el PDF de materialidad por tarea.
/// </summary>
public interface IReporteMaterialidadService
{
    Task<byte[]> GenerarPdfPorTareaAsync(int tareaId);
}