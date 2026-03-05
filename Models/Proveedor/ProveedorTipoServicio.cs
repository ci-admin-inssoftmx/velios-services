using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorTipoServicio", Schema = "dbo")]
public class ProveedorTipoServicio
{
    [Key]
    public long ProveedorTipoServicioId { get; set; }

    public int ProveedorId { get; set; }
    public int TipoServicioId { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; }
}