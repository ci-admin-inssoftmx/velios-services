namespace velios.Api.Models.ServiciosCategoria
{
    public class SubcategoriaServicioModel
    {
        public int SubcategoriaServicioId { get; set; }
        public string SubcategoriaServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; } // ← agregar

    }
}