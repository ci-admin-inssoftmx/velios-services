namespace velios.Api.Services;

/// <summary>
/// Contrato del servicio que arma y genera
/// el PDF de materialidad por tarea.
/// </summary>
public interface IReporteMaterialidadService
{
    public Task<byte[]> GenerarPdfPorTareaAsync(int tareaId, IProgress<(int Procesados, int Total)>? progreso = null);
}