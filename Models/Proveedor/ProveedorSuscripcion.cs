using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorSuscripcion", Schema = "dbo")]
public class ProveedorSuscripcion
{
    [Key]
    public long ProveedorSuscripcionId { get; set; }

    public int ProveedorId { get; set; }
    public int PaqueteId { get; set; }

    public int EstatusSuscripcionId { get; set; } // 1 Activa, 2 Suspendida, 3 Cancelada
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? Motivo { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; }
}