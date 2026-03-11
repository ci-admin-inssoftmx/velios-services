using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("tb_TareaTimeline", Schema = "dbo")]
public class TareaTimeline
{
    [Key]
    [Column("TimelineId")]
    public int TimelineId { get; set; }

    [Column("TareaId")]
    public int TareaId { get; set; }

    [Column("TipoEventoTareaId")]
    public int TipoEventoTareaId { get; set; }

    [Column("Descripcion")]
    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [Column("ValorAnterior")]
    [MaxLength(100)]
    public string? ValorAnterior { get; set; }

    [Column("ValorNuevo")]
    [MaxLength(100)]
    public string? ValorNuevo { get; set; }

    [Column("PerformedBy")]
    [MaxLength(100)]
    public string PerformedBy { get; set; } = string.Empty;

    [Column("PerformedAt")]
    public DateTime PerformedAt { get; set; }

    [Column("DateCreated")]
    public DateTime DateCreated { get; set; }
}