namespace velios.Api.Models.Security;

public class AccesoUsuarioColaborador
{
    public int IdAccesoUsuarioColaborador { get; set; }
    public int IdEmpleado { get; set; }
    public string UsuarioColaborador { get; set; } = "";
    public string? ContraseniaColaborador { get; set; }
    public string PasswordEncriptado { get; set; } = "";
    public int AvisoPrivacidad { get; set; }
    public DateTime? FechaAvisoPrivacidad { get; set; }
    public DateTime? FechaUltimoAcceso { get; set; }
    public int EstatusCambioContrasena { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int Intentos { get; set; }
    public int TipoAcceso { get; set; }
    public int IdEstatus { get; set; }
}