namespace velios.Api.Models.Auth;

/// <summary>
/// Información devuelta después de un login exitoso.
/// 
/// Contiene los datos principales del usuario autenticado,
/// su información laboral, unidad organizacional,
/// roles del sistema y el token de acceso.
/// </summary>
public class LoginDataResponse
{
    /// <summary>
    /// Correo electrónico del usuario autenticado.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Identificador único del usuario dentro del sistema de identidad.
    /// </summary>
    public string IdUsuario { get; set; } = "";

    /// <summary>
    /// Identificador del empleado asociado al usuario.
    /// </summary>
    public int EmpleadoId { get; set; }

    /// <summary>
    /// Número interno de empleado dentro de la organización.
    /// </summary>
    public string NumeroEmpleado { get; set; } = "";

    /// <summary>
    /// Nombre(s) del empleado.
    /// </summary>
    public string Nombres { get; set; } = "";

    /// <summary>
    /// Apellido paterno del empleado.
    /// </summary>
    public string ApellidoPaterno { get; set; } = "";

    /// <summary>
    /// Apellido materno del empleado.
    /// </summary>
    public string ApellidoMaterno { get; set; } = "";

    /// <summary>
    /// Identificador de la unidad organizacional a la que pertenece el empleado.
    /// </summary>
    public int IdUnidad { get; set; }

    /// <summary>
    /// Nombre de la unidad organizacional.
    /// </summary>
    public string NombreUnidad { get; set; } = "";

    /// <summary>
    /// Tipo de unidad organizacional (Ejemplo: Planta, Oficina, Centro de trabajo).
    /// </summary>
    public string NombreTipoUnidad { get; set; } = "";

    /// <summary>
    /// Identificador del horario laboral asignado al empleado.
    /// </summary>
    public int HorarioId { get; set; }

    /// <summary>
    /// Lista de horarios configurados para el empleado.
    /// </summary>
    public List<HorarioDto> Horarios { get; set; } = new();

    /// <summary>
    /// Dirección física de la unidad organizacional.
    /// </summary>
    public UnidadDireccionDto UnidadDireccion { get; set; } = new();

    /// <summary>
    /// Latitud geográfica de la unidad o ubicación del empleado.
    /// 
    /// Representa la coordenada norte-sur en el sistema WGS84.
    /// Ejemplo: 19.432608
    /// </summary>
    public decimal? Latitud { get; set; }

    /// <summary>
    /// Longitud geográfica de la unidad o ubicación del empleado.
    /// 
    /// Representa la coordenada este-oeste en el sistema WGS84.
    /// Ejemplo: -99.133209
    /// </summary>
    public decimal? Longitud { get; set; }

    /// <summary>
    /// Lista de roles asignados al usuario dentro del sistema.
    /// 
    /// Ejemplo:
    /// - Administrador
    /// - Supervisor
    /// - Operador
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Información del token de autenticación generado para la sesión.
    /// </summary>
    public TokenResponse Token { get; set; } = new();
}