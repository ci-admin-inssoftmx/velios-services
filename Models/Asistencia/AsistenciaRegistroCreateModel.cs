using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Asistencia;

/// <summary>
/// Modelo de entrada para crear un registro de asistencia.
/// </summary>
public class AsistenciaRegistroCreateModel
{
    /// <summary>
    /// Identificador del trabajador.
    /// </summary>
    [Required]
    public long TrabajadorId { get; set; }

    /// <summary>
    /// Fecha del registro.
    /// </summary>
    [Required]
    public DateTime FechaRegistro { get; set; }

    /// <summary>
    /// Hora de entrada.
    /// </summary>
    public TimeSpan? HoraEntrada { get; set; }

    /// <summary>
    /// Hora de salida.
    /// </summary>
    public TimeSpan? HoraSalida { get; set; }

    /// <summary>
    /// Tipo de registro.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string? TipoRegistro { get; set; }

    /// <summary>
    /// Origen del registro.
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string? Origen { get; set; }

    /// <summary>
    /// Latitud.
    /// </summary>
    public decimal? Latitud { get; set; }

    /// <summary>
    /// Longitud.
    /// </summary>
    public decimal? Longitud { get; set; }

    /// <summary>
    /// Observación.
    /// </summary>

    public int? CentroTrabajoId { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}