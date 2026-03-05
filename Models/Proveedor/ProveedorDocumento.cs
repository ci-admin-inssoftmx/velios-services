using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_ProveedorDocumentos", Schema = "dbo")]
public class ProveedorDocumento
{
    [Key]
    public long ProveedorDocumentoId { get; set; }

    public int ProveedorId { get; set; }
    public int TipoDocumentoId { get; set; }

    [MaxLength(260)]
    public string NombreOriginal { get; set; } = "";

    [MaxLength(260)]
    public string NombreAlmacenado { get; set; } = "";

    [MaxLength(20)]
    public string Extension { get; set; } = "";

    [MaxLength(100)]
    public string ContentType { get; set; } = "";

    public long TamanoBytes { get; set; }

    [MaxLength(64)]
    public string? Sha256 { get; set; }

    [MaxLength(400)]
    public string RutaRelativa { get; set; } = "";

    public int EstatusDocumentoId { get; set; } = 1;
    [MaxLength(500)]
    public string? Observaciones { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
    /// <summary>
    /// Fecha de fin de vigencia del documento (obligatoria cuando el tipo lo requiere).
    /// </summary>
    public DateTime? FechaFinVigencia { get; set; }

}