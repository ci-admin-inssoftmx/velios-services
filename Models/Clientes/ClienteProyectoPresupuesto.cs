using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_ClienteProyectoPresupuesto", Schema = "dbo")]
public class ClienteProyectoPresupuesto
{
    [Key]
    public int PresupuestoId { get; set; }

    public int ProyectoId { get; set; }

    public int? ProveedorId { get; set; }
    public int? TipoServicioId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Monto { get; set; }

    [MaxLength(10)]
    public string Moneda { get; set; } = "MXN";

    public int EstatusPresupuestoId { get; set; } = 1; // 1=Pendiente 2=Aprobado 3=Rechazado 4=Cancelado

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
}