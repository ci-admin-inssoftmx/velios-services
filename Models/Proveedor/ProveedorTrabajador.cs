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

    // --- NUEVOS CAMPOS ---
    [Column("tipo_de_miembro")]
    public string? TipoDeMiembro { get; set; } // 'Trabajador' o 'Supervisor'

    [Column("nivel")]
    public string? Nivel { get; set; }         // 'Junior', 'Semi', 'Senior'

    [Column("clientes")]
    public string? Clientes { get; set; }

    [Column("centros_de_trabajo")]
    public string? CentroDeTrabajo { get; set; }
    // ---------------------

    public int EstatusTrabajadorId { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }

    [Column("PasswordHash")]
    public string? PasswordHash { get; set; }
}