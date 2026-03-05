namespace velios.Api.Models.Empleado;

public class Empleado
{
    public int IdEmpleado { get; set; }
    public string NoColaborador { get; set; } = "";
    public string ApellidoPaterno { get; set; } = "";
    public string ApellidoMaterno { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Calle { get; set; } = "";
    public string Numero { get; set; } = "";
    public int? IdCP { get; set; }
    public string? EMail { get; set; }
}