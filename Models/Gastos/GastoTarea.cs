using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("tb_GastosTarea", Schema = "dbo")]
public class GastoTarea
{
    [Key]
    public int IdGastoTarea { get; set; }

    public int IdTarea { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Gasto { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}