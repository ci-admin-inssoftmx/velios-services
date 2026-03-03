namespace velios.Api.Models.Auth;

public class LoginDataResponse
{
    public string Email { get; set; } = "";
    public string IdUsuario { get; set; } = "";
    public int EmpleadoId { get; set; }
    public string NumeroEmpleado { get; set; } = "";

    public string Nombres { get; set; } = "";
    public string ApellidoPaterno { get; set; } = "";
    public string ApellidoMaterno { get; set; } = "";

    public int IdUnidad { get; set; }
    public string NombreUnidad { get; set; } = "";
    public string NombreTipoUnidad { get; set; } = "";

    public int HorarioId { get; set; }

    public List<HorarioDto> Horarios { get; set; } = new();

    public UnidadDireccionDto UnidadDireccion { get; set; } = new();

    public List<string> Roles { get; set; } = new();

    public TokenResponse Token { get; set; } = new();
}