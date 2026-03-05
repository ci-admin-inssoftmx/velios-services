using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorPresupuesto", Schema = "dbo")]
public class ProveedorPresupuesto
{
    [Key]
    public long PresupuestoId { get; set; }

    public long ProveedorProyectoId { get; set; }
    public int ProveedorId { get; set; }

    public int Version { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "MXN";
    public string? Descripcion { get; set; }

    public int EstatusPresupuestoId { get; set; } // 1 Enviado, 2 Aprobado, 3 Rechazado, 4 Cancelado
    public DateTime FechaEnvio { get; set; }
    public DateTime? FechaDecision { get; set; }
    public string? MotivoDecision { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}