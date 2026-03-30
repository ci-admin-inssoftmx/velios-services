namespace velios.Api.Models.Common;

/// <summary>
/// Modelo genérico estandarizado para las respuestas de la API.
///
/// Permite mantener una estructura uniforme en todas las respuestas
/// HTTP del sistema, facilitando el consumo por parte de frontend,
/// aplicaciones móviles o integraciones externas.
///
/// Ventajas:
/// - Trazabilidad mediante request_id.
/// - Indicador explícito de éxito o error.
/// - Manejo centralizado de errores.
/// - Compatible con cualquier tipo de dato mediante genéricos.
/// </summary>
/// <typeparam name="T">
/// Tipo de dato que contendrá la propiedad <see cref="data"/>.
/// Puede ser un objeto, lista, valor simple o null.
/// </typeparam>
public class ApiResponse<T>
{
        // 1. Agregamos esta línea (es el "folio" de seguimiento)
        public string request_id { get; set; } = Guid.NewGuid().ToString();    /// <summary>
    /// Indica si la operación fue exitosa.
    /// 
    /// true  = Operación completada correctamente.
    /// false = Ocurrió un error.
    /// </summary>
    public bool success { get; set; }

    /// <summary>
    /// Mensaje descriptivo del resultado de la operación.
    /// Puede representar éxito o motivo del error.
    /// </summary>
    public string message { get; set; } = "";

    /// <summary>
    /// Información devuelta por la operación.
    /// 
    /// Puede ser:
    /// - Un objeto
    /// - Una colección
    /// - Un identificador
    /// - null (cuando no aplica)
    /// </summary>
    public T? data { get; set; }

    /// <summary>
    /// Código HTTP asociado a la respuesta.
    /// 
    /// Ejemplos:
    /// 200 = OK
    /// 400 = Bad Request
    /// 404 = Not Found
    /// 409 = Conflict
    /// 500 = Internal Server Error
    /// </summary>
    public int statusCode { get; set; }

    /// <summary>
    /// Nombre del campo que causó el error (opcional).
    /// 
    /// Útil para validaciones específicas.
    /// </summary>
    public string? field { get; set; }

    /// <summary>
    /// Código interno del error (opcional).
    /// 
    /// Permite clasificar errores de forma programática.
    /// Ejemplo: "EMAIL_DUPLICADO", "PROYECTO_CERRADO".
    /// </summary>
    public string? code { get; set; }

    /// <summary>
    /// Lista de errores detallados (opcional).
    /// 
    /// Se utiliza cuando hay múltiples validaciones fallidas.
    /// </summary>
    public List<string>? errors { get; set; }
}