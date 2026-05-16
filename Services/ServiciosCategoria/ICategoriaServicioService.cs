using velios.Api.Models.ServiciosCategoria;

namespace velios.Api.Services.ServiciosCategoria
{
    public interface ICategoriaServicioService
    {
        // ── Catálogos ──────────────────────────────────────────
        Task<IEnumerable<CategoriaServicioModel>> GetCategoriasAsync();
        Task<IEnumerable<SubcategoriaServicioModel>> GetSubcategoriasByCategoriaAsync(int categoriaId);
        Task<IEnumerable<ServicioModel>> GetServiciosBySubcategoriaAsync(int subcategoriaId);

        // ── Solicitud ──────────────────────────────────────────
        Task<int> GuardarSolicitudAsync(GuardarSolicitudRequest request);
        Task<bool> EditarSolicitudAsync(EditarSolicitudRequest request);
        Task<SolicitudServicioModel?> GetSolicitudAsync(int solicitudId);
    }
}