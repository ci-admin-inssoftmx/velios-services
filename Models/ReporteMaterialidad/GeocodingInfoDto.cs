namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// Resultado simplificado de geocodificación inversa.
/// </summary>
public class GeocodingInfoDto
{
    public string? DireccionFormateada { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Pais { get; set; }
}