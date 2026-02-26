namespace velios.Api.Models.Asistencia;

public class RegistroAsistenciaCreateRequest
{
    public int IdEmpleado { get; set; }
    public DateTime Fecha { get; set; }
    public int TipoRegistroId { get; set; }
    public TimeSpan? HoraEntrada { get; set; }
    public TimeSpan? HoraSalida { get; set; }
    public string? Observaciones { get; set; }
}