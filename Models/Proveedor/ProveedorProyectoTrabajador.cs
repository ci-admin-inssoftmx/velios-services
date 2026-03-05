using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorProyectoTrabajador", Schema = "dbo")]
public class ProveedorProyectoTrabajador
{
    [Key]
    public long ProveedorProyectoTrabajadorId { get; set; }

    public long ProveedorProyectoId { get; set; }
    public int ProveedorId { get; set; }
    public long TrabajadorId { get; set; }

    public DateTime FechaAsignacion { get; set; }
    public DateTime? FechaBaja { get; set; }
    public string? Motivo { get; set; }

    public bool IsDeleted { get; set; }
}