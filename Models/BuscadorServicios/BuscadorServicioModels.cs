namespace velios.Api.Models.ServiciosCategoria
{
    public class BuscadorServicioResultado
    {
        public IEnumerable<BuscadorCategoriaItem> Categorias { get; set; } = new List<BuscadorCategoriaItem>();
        public IEnumerable<BuscadorSubcategoriaItem> Subcategorias { get; set; } = new List<BuscadorSubcategoriaItem>();
        public IEnumerable<BuscadorServicioItem> Servicios { get; set; } = new List<BuscadorServicioItem>();
    }

    public class BuscadorCategoriaItem
    {
        public int CategoriaServicioId { get; set; }
        public string CategoriaServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class BuscadorSubcategoriaItem
    {
        public int SubcategoriaServicioId { get; set; }
        public string SubcategoriaServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int CategoriaServicioId { get; set; }
        public string CategoriaServicio { get; set; } = string.Empty;
    }

    public class BuscadorServicioItem
    {
        public int ServicioId { get; set; }
        public string Servicio { get; set; } = string.Empty;
        public int SubcategoriaServicioId { get; set; }
        public string SubcategoriaServicio { get; set; } = string.Empty;
        public int CategoriaServicioId { get; set; }
        public string CategoriaServicio { get; set; } = string.Empty;
    }
    // ============================================================
    // Respuesta jerárquica del buscador
    // ============================================================
    public class BuscadorJerarquiaResultado
    {
        public IEnumerable<CategoriaJerarquiaItem> Jerarquia { get; set; } = new List<CategoriaJerarquiaItem>();
    }

    public class CategoriaJerarquiaItem
    {
        public int CategoriaId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public List<SubcategoriaJerarquiaItem> Subcategorias { get; set; } = new();
    }

    public class SubcategoriaJerarquiaItem
    {
        public int SubcategoriaId { get; set; }
        public string Subcategoria { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public List<ServicioJerarquiaItem> Servicios { get; set; } = new();
    }

    public class ServicioJerarquiaItem
    {
        public int ServicioId { get; set; }
        public string Servicio { get; set; } = string.Empty;
    }
}