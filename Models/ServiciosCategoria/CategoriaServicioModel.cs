namespace velios.Api.Models.ServiciosCategoria
{
    public class CategoriaServicioModel
    {
        public int CategoriaServicioId { get; set; }
        public string CategoriaServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; } // ← agregar

    }
}
