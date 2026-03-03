using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorProyecto")]
public class ProveedorProyecto
{
    [Key]
    public long ProveedorProyectoId { get; set; }

    public int ProveedorId { get; set; }

    [Required]
    public string ProyectoNombre { get; set; } = "";

    public string? ClienteNombre { get; set; }

    public DateTime FechaAsignacion { get; set; }
    public DateTime? FechaRespuesta { get; set; }

    public int EstatusProyectoProveedorId { get; set; }

    public string? Observaciones { get; set; }

    public bool IsDeleted { get; set; }
}