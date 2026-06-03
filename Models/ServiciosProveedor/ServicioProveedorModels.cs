namespace velios.Api.Models.ServiciosProveedor
{
    // ============================================================
    // Request: Asignar servicios a un proveedor (múltiple)
    // ============================================================
    public class AsignarServiciosProveedorRequest
    {
        public int ProveedorId { get; set; }
        public List<int> ServicioIds { get; set; } = new();
    }

    // ============================================================
    // Response: Servicio asignado a un proveedor
    // ============================================================
    public class ServicioProveedorModel
    {
        public int ProveedorServicioId { get; set; }
        public int ProveedorId { get; set; }
        public int CategoriaServicioId { get; set; }
        public string CategoriaServicio { get; set; } = string.Empty;
        public int SubcategoriaServicioId { get; set; }
        public string SubcategoriaServicio { get; set; } = string.Empty;
        public int ServicioId { get; set; }
        public string Servicio { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }

    // ============================================================
    // Response: Resultado de asignación múltiple
    // ============================================================
    public class AsignarServiciosResultado
    {
        public int Insertados { get; set; }
        public int Duplicados { get; set; }
        public List<int> ServiciosDuplicados { get; set; } = new();
    }
}