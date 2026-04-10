namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// DTO con la información del cliente que se mostrará
/// en la portada o primera hoja del reporte.
/// </summary>
public class ClienteReporteDto
{
    public int ClienteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? RFC { get; set; }
}