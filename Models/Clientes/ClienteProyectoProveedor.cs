using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_ClienteProyectoProveedor", Schema = "dbo")]
public class ClienteProyectoProveedor
{
    [Key]
    public int ClienteProyectoProveedorId { get; set; }

    public int ProyectoId { get; set; }
    public int ProveedorId { get; set; }
    public int TipoServicioId { get; set; }

    public bool ActivoAsignacion { get; set; } = true;

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
}