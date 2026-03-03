using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_ClienteProveedor", Schema = "dbo")]
public class ClienteProveedor
{
    [Key]
    public int ClienteProveedorId { get; set; }

    public int ClienteId { get; set; }
    public int ProveedorId { get; set; }

    public int EstatusRelacionId { get; set; } = 2; // 1=APROBADO,2=SUSPENDIDO,3=BAJA
    [MaxLength(500)]
    public string? Notas { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
}