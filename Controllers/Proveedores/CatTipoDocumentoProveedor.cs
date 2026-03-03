using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

/// <summary>
/// Catálogo de tipos de documentos requeridos para proveedores.
/// Define las reglas de carga (obligatorio, vigencia, formatos permitidos y tamaño máximo).
/// 
/// Tabla: dbo.tb_CatTipoDocumentoProveedor
/// </summary>
[Table("tb_CatTipoDocumentoProveedor", Schema = "dbo")]
public class CatTipoDocumentoProveedor
{
    /// <summary>
    /// Identificador del tipo de documento (PK).
    /// </summary>
    [Key]
    public int TipoDocumentoId { get; set; }

    /// <summary>
    /// Código único del documento (ej: CSF, ACTA_CONST, PODER_REP).
    /// Útil para lógica del sistema y front-end.
    /// </summary>
    [Required, MaxLength(50)]
    public string Codigo { get; set; } = "";

    /// <summary>
    /// Nombre visible del documento (ej: Constancia de Situación Fiscal).
    /// </summary>
    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";

    /// <summary>
    /// Descripción opcional para ayudar al usuario/administrador.
    /// </summary>
    [MaxLength(300)]
    public string? Descripcion { get; set; }

    /// <summary>
    /// Indica si este documento es obligatorio durante el alta/configuración inicial del proveedor.
    /// </summary>
    public bool ObligatorioAlta { get; set; }

    /// <summary>
    /// Indica si este tipo de documento requiere fecha de fin de vigencia.
    /// Si es true, el upload debe enviar FechaFinVigencia.
    /// </summary>
    public bool RequiereVigencia { get; set; }

    /// <summary>
    /// Tamaño máximo permitido en bytes (ej: 15728640 = 15MB).
    /// </summary>
    public long MaxBytes { get; set; }

    /// <summary>
    /// Permite subir PDF para este tipo de documento.
    /// </summary>
    public bool PermitePdf { get; set; }

    /// <summary>
    /// Permite subir imágenes para este tipo de documento (JPG/PNG).
    /// </summary>
    public bool PermiteImagen { get; set; }

    /// <summary>
    /// Indica si el tipo de documento está activo en el catálogo.
    /// </summary>
    public bool Activo { get; set; }

    /// <summary>
    /// Soft delete del registro del catálogo.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Fecha de creación del registro (UTC).
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}