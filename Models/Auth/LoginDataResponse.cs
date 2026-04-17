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
    /// Identificador del id de proveedor.
    /// </summary>
    public int ProveedorID { get; set; } = 0;

    /// <summary>
    /// Nombre del proveedor (Nombre comercial o razón social).
    /// Se retorna inmediatamente después de `ProveedorID` en la respuesta.
    /// </summary>
    public string ProveedorNombre { get; set; } = "";

    /// <summary>
    /// Correo electrónico del usuario autenticado.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Nombre(s) del empleado.
    /// </summary>
    public string NombreCompleto { get; set; } = "";

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

    public long? TrabajadorId { get; set; }
}