using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("tb_TareaObservaciones", Schema = "dbo")]
public class TareaObservacion
{
    [Key]
    [Column("ObservacionId")]
    public int ObservacionId { get; set; }

    [Column("TareaId")]
    public int TareaId { get; set; }

    [Column("Observacion")]
    [MaxLength(1000)]
    public string Observacion { get; set; } = string.Empty;

    [Column("CreatedBy")]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Column("DateCreated")]
    public DateTime DateCreated { get; set; }
}