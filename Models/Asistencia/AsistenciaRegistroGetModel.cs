using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Asistencia;

/// <summary>
/// Modelo de entrada para consultar registros de asistencia.
/// </summary>
public class AsistenciaRegistroGetModel
{
    /// <summary>
    /// Identificador del trabajador.
    /// </summary>
    [Required]
    public long TrabajadorId { get; set; }

    /// <summary>
    /// Fecha inicial del filtro.
    /// </summary>
    [Required]
    public DateTime FechaInicio { get; set; }

    /// <summary>
    /// Fecha final del filtro.
    /// </summary>
    [Required]
    public DateTime FechaFin { get; set; }

    /// <summary>
    /// Origen.
    /// </summary>
    public string? Origen { get; set; }

    /// <summary>
    /// Tipo de registro.
    /// </summary>
    public string? TipoRegistro { get; set; }
}