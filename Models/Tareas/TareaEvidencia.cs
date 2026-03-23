using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("tb_TareaEvidencias", Schema = "dbo")]
public class TareaEvidencia
{
    [Key]
    [Column("EvidenciaId")]
    public int EvidenciaId { get; set; }

    [Column("TareaId")]
    public int TareaId { get; set; }

    [Column("Tipo")]
    [MaxLength(50)] // Cambiado de 20 a 50
    public string Tipo { get; set; } = string.Empty;
    [Column("UrlArchivo")]
    [MaxLength(500)]
    public string? UrlArchivo { get; set; }

    [Column("MimeType")]
    [MaxLength(100)]
    public string? MimeType { get; set; }

    [Column("SizeBytes")]
    public long? SizeBytes { get; set; }

    [Column("Latitud", TypeName = "decimal(10,8)")]
    public decimal? Latitud { get; set; }

    [Column("Longitud", TypeName = "decimal(11,8)")]
    public decimal? Longitud { get; set; }

    [Column("Direccion")]
    [MaxLength(500)]
    public string? Direccion { get; set; }

    [Column("Plataforma")]
    [MaxLength(20)]
    public string? Plataforma { get; set; }

    [Column("VersionApp")]
    [MaxLength(20)]
    public string? VersionApp { get; set; }

    [Column("ModeloDispositivo")]
    [MaxLength(100)]
    public string? ModeloDispositivo { get; set; }

    [Column("VersionOS")]
    [MaxLength(50)]
    public string? VersionOS { get; set; }

    [Column("DateCreated")]
    public DateTime DateCreated { get; set; }
}