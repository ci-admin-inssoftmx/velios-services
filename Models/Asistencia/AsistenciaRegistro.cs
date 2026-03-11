using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Asistencia;

/// <summary>
/// Entidad de base de datos para la tabla dbo.tb_AsistenciaRegistros.
/// </summary>
[Table("tb_AsistenciaRegistros", Schema = "dbo")]
public class AsistenciaRegistro
{
    /// <summary>
    /// Clave primaria del registro de asistencia.
    /// </summary>
    [Key]
    [Column("AsistenciaRegistroId")]
    public int AsistenciaRegistroId { get; set; }

    /// <summary>
    /// Identificador del trabajador.
    /// </summary>
    [Column("TrabajadorId")]
    public long TrabajadorId { get; set; }

    /// <summary>
    /// Fecha del registro.
    /// </summary>
    [Column("Fecha")]
    public DateTime Fecha { get; set; }

    /// <summary>
    /// Hora de entrada.
    /// </summary>
    [Column("HoraEntrada")]
    public TimeSpan? HoraEntrada { get; set; }

    /// <summary>
    /// Hora de salida.
    /// </summary>
    [Column("HoraSalida")]
    public TimeSpan? HoraSalida { get; set; }

    /// <summary>
    /// Tipo de registro.
    /// </summary>
    [Column("TipoRegistro")]
    [MaxLength(50)]
    public string TipoRegistro { get; set; } = string.Empty;

    /// <summary>
    /// Origen del registro.
    /// </summary>
    [Column("Origen")]
    [MaxLength(10)]
    public string Origen { get; set; } = string.Empty;

    /// <summary>
    /// Latitud de ubicación.
    /// </summary>
    [Column("Latitud", TypeName = "decimal(10,8)")]
    public decimal? Latitud { get; set; }

    /// <summary>
    /// Longitud de ubicación.
    /// </summary>
    [Column("Longitud", TypeName = "decimal(11,8)")]
    public decimal? Longitud { get; set; }

    /// <summary>
    /// Observación del registro.
    /// </summary>
    [Column("Observacion")]
    [MaxLength(500)]
    public string? Observacion { get; set; }

    /// <summary>
    /// Usuario creador.
    /// </summary>
    [Column("CreatedBy")]
    [MaxLength(450)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Usuario modificador.
    /// </summary>
    [Column("ModifiedBy")]
    [MaxLength(450)]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Fecha de creación.
    /// </summary>
    [Column("DateCreated")]
    public DateTime DateCreated { get; set; }

    /// <summary>
    /// Fecha de modificación.
    /// </summary>
    [Column("DateModified")]
    public DateTime? DateModified { get; set; }

    /// <summary>
    /// Borrado lógico.
    /// </summary>
    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }
}