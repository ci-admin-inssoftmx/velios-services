using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class PresupuestoDecisionRequest
{
    [Required]
    public int EstatusPresupuestoId { get; set; } // 2 Aprobado, 3 Rechazado

    public string? MotivoDecision { get; set; }
}