namespace velios.Api.Models.PresupuestoGuardado
{
    // ============================================================
    // Request: Guardar presupuesto
    // ============================================================
    public class GuardarPresupuestoRequest
    {
        public int TareaId { get; set; }
        public decimal PresupuestoDisponible { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }

    // ============================================================
    // Response: Presupuesto guardado
    // ============================================================
    public class PresupuestoGuardadoModel
    {
        public int PresupuestoGuardadoId { get; set; }
        public int TareaId { get; set; }
        public decimal PresupuestoAsignado { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal PresupuestoDisponible { get; set; }
        public DateTime FechaLlenado { get; set; }
    }
}