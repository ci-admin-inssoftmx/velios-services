using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("tb_Tareas", Schema = "dbo")]
public class Tarea
{
    [Key]
    [Column("TareaId")]
    public int TareaId { get; set; }

    [Column("TaskCode")]
    [MaxLength(50)]
    public string TaskCode { get; set; } = string.Empty;

    [Column("Titulo")]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Column("Descripcion")]
    public string? Descripcion { get; set; }

    [Column("ClienteId")]
    public int ClienteId { get; set; }

    [Column("ProyectoId")]
    public int? ProyectoId { get; set; }

    [Column("EstatusTareaId")]
    public int EstatusTareaId { get; set; }

    [Column("TrabajadorId")]
    public long? TrabajadorId { get; set; }

    [Column("FechaAsignacion")]
    public DateTime FechaAsignacion { get; set; }

    [Column("FechaProgramada")]
    public DateTime? FechaProgramada { get; set; }

    [Column("FechaVencimiento")]
    public DateTime FechaVencimiento { get; set; }

    [Column("PresupuestoAsignado", TypeName = "decimal(18,2)")]
    public decimal? PresupuestoAsignado { get; set; }

    [Column("Moneda")]
    [MaxLength(10)]
    public string Moneda { get; set; } = "MXN";

    [Column("DateCreated")]
    public DateTime DateCreated { get; set; }

    [Column("DateModified")]
    public DateTime? DateModified { get; set; }

    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }

    [Column("ImagenUrl")]
    [MaxLength(500)]
    public string? ImagenUrl { get; set; }
}