using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_CentrosTrabajo", Schema = "dbo")]
public class CentroTrabajo
{
    [Key]
    public int CentroTrabajoId { get; set; }

    public int ClienteId { get; set; }

    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";

    [MaxLength(100)] public string? Estado { get; set; }
    [MaxLength(100)] public string? Zona { get; set; }
    [MaxLength(100)] public string? Territorio { get; set; }
    [MaxLength(100)] public string? Region { get; set; }

    [MaxLength(150)] public string? Calle { get; set; }
    [MaxLength(50)] public string? Numero { get; set; }
    [MaxLength(100)] public string? Colonia { get; set; }
    [MaxLength(100)] public string? Municipio { get; set; }
    [MaxLength(10)] public string? CodigoPostal { get; set; }

    [Column(TypeName = "decimal(10,7)")]
    public decimal? Lat { get; set; }

    [Column(TypeName = "decimal(10,7)")]
    public decimal? Lng { get; set; }

    [MaxLength(20)]
    public string? TipoGeocerca { get; set; } // RADIO | POLIGONO

    public int? RadioMetros { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
}