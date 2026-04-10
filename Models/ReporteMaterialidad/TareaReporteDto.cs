namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// DTO con la información principal de una tarea.
/// </summary>
public class TareaReporteDto
{
    public int TareaId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public int? ProyectoId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public int EstatusTareaId { get; set; }
    public string EstatusCodigo { get; set; } = string.Empty;
    public string EstatusNombre { get; set; } = string.Empty;

    public long? TrabajadorId { get; set; }
    public long? SupervisorId { get; set; }

    public string? NombreOperador { get; set; }
    public string? NombreSupervisor { get; set; }

    public DateTime FechaAsignacion { get; set; }
    public DateTime? FechaProgramada { get; set; }
    public DateTime FechaVencimiento { get; set; }

    public decimal? PresupuestoAsignado { get; set; }
    public string Moneda { get; set; } = string.Empty;

    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }

    public bool IsDeleted { get; set; }
    public string? ImageURL { get; set; }
    public int? CentroTrabajoId { get; set; }

    public List<EvidenciaReporteDto> Evidencias { get; set; } = new();
}