namespace velios.Api.Models.Auth;

public class HorarioDto
{
    public int diaSemanaId { get; set; }
    public string diaSemana { get; set; } = "";
    public string horaEntrada { get; set; } = "";
    public string horaSalida { get; set; } = "";
}