namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// DTO con la información principal de una tarea.
/// </summary>
public class TareaReporteDto
{
    public int TareaId { get; set; }
    public string? NombreProyecto { get; set; }

    public string? NombreCentroTrabajo { get; set; }

    public string? LogoUrlProveedor { get; set; }
    public string? NombreProveedor { get; set; }

    public string? DireccionCentroTrabajo { get; set; }

    public string? EmailSupervisor { get; set; }

    public string TaskCode { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public int? ProyectoId { get; set; }
    public List<string> Observaciones { get; set; } = new();

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
    public decimal? PresupuestoUsado { get; set; }
    public decimal? PresupuestoDisponible { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }

    public bool IsDeleted { get; set; }
    public string? ImageURL { get; set; }
    public int? CentroTrabajoId { get; set; }

    public string? TelefonoCentroTrabajo { get; set; }
    public List<EvidenciaReporteDto> Evidencias { get; set; } = new();
}