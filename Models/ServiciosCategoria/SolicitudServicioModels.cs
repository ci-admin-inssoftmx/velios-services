namespace velios.Api.Models.ServiciosCategoria
{
    // ============================================================
    // Request: Guardar solicitud
    // ============================================================
    public class GuardarSolicitudRequest
    {
        public int TareaId { get; set; }
        public int ServicioId { get; set; }
        public int ClienteId { get; set; }
    }

    // ============================================================
    // Request: Editar solicitud (solo ServicioId)
    // ============================================================
    public class EditarSolicitudRequest
    {
        public int SolicitudId { get; set; }
        public int ServicioId { get; set; }
    }

    // ============================================================
    // Response: Consulta de solicitud
    // ============================================================
    public class SolicitudServicioModel
    {
        public int SolicitudId { get; set; }
        public int TareaId { get; set; }
        public int ClienteId { get; set; }
        public int CategoriaServicioId { get; set; }
        public string CategoriaServicio { get; set; } = string.Empty;
        public int SubcategoriaServicioId { get; set; }
        public string SubcategoriaServicio { get; set; } = string.Empty;
        public int ServicioId { get; set; }
        public string Servicio { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }
}