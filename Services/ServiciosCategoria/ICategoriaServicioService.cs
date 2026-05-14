using velios.Api.Models.ServiciosCategoria;

namespace velios.Api.Services.ServiciosCategoria
{
    public interface ICategoriaServicioService
    {
        Task<IEnumerable<CategoriaServicioModel>> GetCategoriasAsync();
        Task<IEnumerable<SubcategoriaServicioModel>> GetSubcategoriasByCategoriaAsync(int categoriaId); // ← agregar
        Task<IEnumerable<ServicioModel>> GetServiciosBySubcategoriaAsync(int subcategoriaId);

    }
}