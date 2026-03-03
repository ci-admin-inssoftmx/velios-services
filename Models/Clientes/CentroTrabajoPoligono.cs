using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_CentroTrabajoPoligono", Schema = "dbo")]
public class CentroTrabajoPoligono
{
    [Key]
    public int CentroTrabajoPoligonoId { get; set; }

    public int CentroTrabajoId { get; set; }
    public int Orden { get; set; }

    [Column(TypeName = "decimal(10,7)")]
    public decimal Lat { get; set; }

    [Column(TypeName = "decimal(10,7)")]
    public decimal Lng { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}