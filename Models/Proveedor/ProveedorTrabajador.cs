using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorTrabajadores", Schema = "dbo")]
public class ProveedorTrabajador
{
    [Key]
    public long TrabajadorId { get; set; }

    public int ProveedorId { get; set; }

    [Required]
    public string Nombre { get; set; } = "";

    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }

    public string? CURP { get; set; }
    public string? RFC { get; set; }
    public string? NSS { get; set; }

    public string? Correo { get; set; }
    public string? Telefono { get; set; }

    public int EstatusTrabajadorId { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}