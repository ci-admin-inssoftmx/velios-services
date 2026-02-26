using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_Proveedores", Schema = "dbo")]
public class Proveedor
{
    [Key]
    public int ProveedorId { get; set; }

    [Required, MaxLength(20)]
    public string RFC { get; set; } = "";

    [Required, MaxLength(250)]
    public string RazonSocial { get; set; } = "";

    [MaxLength(250)]
    public string? NombreComercial { get; set; }

    [Required, MaxLength(150)]
    public string CorreoContacto { get; set; } = "";

    [MaxLength(20)]
    public string? TelefonoContacto { get; set; }

    [MaxLength(250)]
    public string? RepresentanteLegal { get; set; }

    public int EstatusProveedorId { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public bool? IsDeleted { get; set; }  // ✅ Nullable
}