using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

/// <summary>
/// Entidad que representa un Proveedor dentro del sistema.
///
/// Esta tabla almacena la información fiscal, comercial y de seguridad
/// asociada a los proveedores registrados en la plataforma.
///
/// Características:
/// - Implementa soft delete mediante el campo IsDeleted.
/// - Incluye auditoría básica (CreatedBy, ModifiedBy, DateCreated, DateModified).
/// - Permite manejo de autenticación básica mediante PasswordHash.
/// - Se mapea a la tabla dbo.tb_Proveedores.
/// </summary>
[Table("tb_Proveedores", Schema = "dbo")]
public class Proveedor
{
    /// <summary>
    /// Identificador único del proveedor.
    /// Clave primaria.
    /// </summary>
    [Key]
    public int ProveedorId { get; set; }

    /// <summary>
    /// Registro Federal de Contribuyentes del proveedor.
    /// Campo obligatorio.
    /// Longitud máxima: 20 caracteres.
    /// </summary>
    [Required, MaxLength(20)]
    public string RFC { get; set; } = "";

    /// <summary>
    /// Razón social registrada legalmente.
    /// Campo obligatorio.
    /// Longitud máxima: 250 caracteres.
    /// </summary>
    [Required, MaxLength(250)]
    public string RazonSocial { get; set; } = "";

    /// <summary>
    /// Nombre comercial del proveedor (opcional).
    /// Longitud máxima: 250 caracteres.
    /// </summary>
    [MaxLength(250)]
    public string? NombreComercial { get; set; }

    /// <summary>
    /// Correo electrónico principal de contacto.
    /// Campo obligatorio.
    /// Longitud máxima: 150 caracteres.
    /// </summary>
    [Required, MaxLength(150)]
    public string CorreoContacto { get; set; } = "";

    /// <summary>
    /// Teléfono de contacto del proveedor (opcional).
    /// Longitud máxima: 20 caracteres.
    /// </summary>
    [MaxLength(20)]
    public string? TelefonoContacto { get; set; }

    /// <summary>
    /// Nombre del representante legal del proveedor (opcional).
    /// Longitud máxima: 250 caracteres.
    /// </summary>
    [MaxLength(250)]
    public string? RepresentanteLegal { get; set; }

    /// <summary>
    /// Identificador del estatus del proveedor.
    ///
    /// Ejemplo de valores:
    /// 1 = Activo
    /// 2 = Suspendido
    /// 3 = Baja
    /// </summary>
    public int EstatusProveedorId { get; set; }

    // ================================
    // Auditoría
    // ================================

    /// <summary>
    /// Usuario que creó el registro.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Usuario que modificó el registro por última vez.
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Fecha de creación del registro (UTC).
    /// </summary>
    public DateTime DateCreated { get; set; }

    /// <summary>
    /// Fecha de última modificación (UTC).
    /// </summary>
    public DateTime? DateModified { get; set; }

    /// <summary>
    /// Indicador de eliminación lógica (soft delete).
    /// 
    /// true  = Eliminado
    /// false = Activo
    /// null  = Valor legacy (considerar como no eliminado)
    /// </summary>
    public bool? IsDeleted { get; set; }

    // ================================
    // Seguridad
    // ================================

    /// <summary>
    /// Hash de la contraseña del proveedor.
    /// 
    /// Se almacena en formato seguro (ej. SHA256 + salt).
    /// Longitud máxima: 255 caracteres.
    /// </summary>
    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Fecha en la que se estableció o actualizó la contraseña.
    /// </summary>
    public DateTime? PasswordSetAt { get; set; }

    // =====================
    // Dirección / Datos legales
    // =====================

    /// <summary>
    /// Calle de la organización.
    /// </summary>
    [MaxLength(250)]
    public string? Calle { get; set; }

    /// <summary>
    /// Código postal (5 dígitos MX generalmente).
    /// </summary>
    [MaxLength(10)]
    public string? CodigoPostal { get; set; }

    /// <summary>
    /// Colonia seleccionada (de SEPOMEX) para el CP.
    /// </summary>
    [MaxLength(150)]
    public string? Colonia { get; set; }

    /// <summary>
    /// Delegación/Municipio (SEPOMEX: D_mnpio).
    /// </summary>
    [MaxLength(150)]
    public string? DelegacionMunicipio { get; set; }

    /// <summary>
    /// Ciudad / localidad (si la usas en UI; puede ser igual a municipio si no hay catálogo de ciudad).
    /// </summary>
    [MaxLength(150)]
    public string? Ciudad { get; set; }

    /// <summary>
    /// Estado (SEPOMEX: d_estado).
    /// </summary>
    [MaxLength(150)]
    public string? Estado { get; set; }

    /// <summary>
    /// País (por default “México” si aplica).
    /// </summary>
    [MaxLength(80)]
    public string? Pais { get; set; }
}