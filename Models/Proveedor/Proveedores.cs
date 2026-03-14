using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

/// <summary>
/// Entidad que representa un proveedor dentro del sistema.
/// 
/// Esta tabla almacena la información fiscal, comercial,
/// de contacto, seguridad y ubicación geográfica del proveedor.
/// </summary>
[Table("tb_Proveedores", Schema = "dbo")]
public class Proveedor
{
    [Key]
    public int ProveedorId { get; set; }

    [MaxLength(20)]
    public string? RFC { get; set; }

    [MaxLength(250)]
    public string? RazonSocial { get; set; }

    [MaxLength(250)]
    public string? NombreComercial { get; set; }

    [Required, MaxLength(150)]
    public string CorreoContacto { get; set; } = "";

    [MaxLength(20)]
    public string? TelefonoContacto { get; set; }

    [MaxLength(250)]
    public string? RepresentanteLegal { get; set; }

    /// <summary>
    /// Estatus del proveedor dentro del sistema.
    /// Ejemplo:
    /// 1 = Activo
    /// 2 = Suspendido
    /// </summary>
    public int? EstatusProveedorId { get; set; }

    // ================================
    // Auditoría
    // ================================

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? DateCreated { get; set; }

    public DateTime? DateModified { get; set; }

    /// <summary>
    /// Indicador de eliminación lógica (soft delete).
    /// </summary>
    public bool IsDeleted { get; set; }

    // ================================
    // Seguridad
    // ================================

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    public DateTime? PasswordSetAt { get; set; }

    // ================================
    // Dirección
    // ================================

    [MaxLength(250)]
    public string? Calle { get; set; }

    [MaxLength(10)]
    public string? CodigoPostal { get; set; }

    [MaxLength(150)]
    public string? Colonia { get; set; }

    [MaxLength(150)]
    public string? DelegacionMunicipio { get; set; }

    [MaxLength(150)]
    public string? Ciudad { get; set; }

    [MaxLength(150)]
    public string? Estado { get; set; }

    [MaxLength(80)]
    public string? Pais { get; set; }

    /// <summary>
    /// URL del logotipo del proveedor almacenado en el sistema.
    /// </summary>
    public string? LogoUrl { get; set; }

    // ================================
    // Geolocalización
    // ================================

    /// <summary>
    /// Latitud geográfica del proveedor.
    /// 
    /// Representa la coordenada norte-sur en el sistema WGS84.
    /// 
    /// Ejemplo:
    /// 19.432608
    /// </summary>
    public decimal? Latitud { get; set; }

    /// <summary>
    /// Longitud geográfica del proveedor.
    /// 
    /// Representa la coordenada este-oeste en el sistema WGS84.
    /// 
    /// Ejemplo:
    /// -99.133209
    /// </summary>
    public decimal? Longitud { get; set; }
}