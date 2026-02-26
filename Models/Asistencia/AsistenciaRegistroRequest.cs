using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Asistencia;

public class AsistenciaRegistroRequest
{
    [Required]
    public int IdEmpleado { get; set; }

    public long AsistenciaId { get; set; }

    public int EmpleadoId { get; set; }

    public DateTime Fecha { get; set; }

    public TimeSpan? HoraEntrada { get; set; }

    public TimeSpan? HoraSalida { get; set; }

    public int OrigenId { get; set; }

    public int TipoRegistroId { get; set; }

    public string? Observaciones { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DateCreated { get; set; }
}