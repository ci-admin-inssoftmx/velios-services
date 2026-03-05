using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_ClienteProyectos", Schema = "dbo")]
public class ClienteProyecto
{
    [Key]
    public int ProyectoId { get; set; }

    public int ClienteId { get; set; }

    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";

    [MaxLength(500)]
    public string? Descripcion { get; set; }

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    public int EstatusProyectoId { get; set; } = 1; // 1=Activo 2=Cerrado 3=Cancelado

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
}