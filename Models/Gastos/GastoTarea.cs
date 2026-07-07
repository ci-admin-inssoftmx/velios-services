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

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    [Column("Descripcion")]
    [MaxLength(1000)]
    public string? Descripcion { get; set; }       // ← NUEVO

    [Column("RegisteredById")]
    public int? RegisteredById { get; set; }       // ← NUEVO

    [Column("RegisteredByType")]
    [MaxLength(50)]
    public string? RegisteredByType { get; set; }  // ← NUEVO
}