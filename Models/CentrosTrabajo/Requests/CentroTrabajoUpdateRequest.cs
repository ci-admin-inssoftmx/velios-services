using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.CentrosTrabajo.Requests;

public class CentroTrabajoUpdateRequest
{
    [Required]
    public int ClienteId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Estado { get; set; }

    [MaxLength(100)]
    public string? Zona { get; set; }

    [MaxLength(100)]
    public string? Territorio { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(150)]
    public string? Calle { get; set; }

    [MaxLength(50)]
    public string? Numero { get; set; }

    [MaxLength(100)]
    public string? Colonia { get; set; }

    [MaxLength(100)]
    public string? Municipio { get; set; }

    [MaxLength(10)]
    public string? CodigoPostal { get; set; }

    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }

    [MaxLength(20)]
    public string? TipoGeocerca { get; set; }

    public int? RadioMetros { get; set; }
}
