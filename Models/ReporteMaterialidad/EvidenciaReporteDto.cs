namespace velios.Api.Models.ReporteMaterialidad;

/// <summary>
/// DTO que representa una evidencia fotográfica
/// asociada a una tarea.
/// </summary>
public class EvidenciaReporteDto
{
    public int EvidenciaId { get; set; }
    public int TareaId { get; set; }

    public string Tipo { get; set; } = string.Empty;
    public string? UrlArchivo { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }

    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public string? Direccion { get; set; }

    public string? Plataforma { get; set; }
    public string? VersionApp { get; set; }
    public string? ModeloDispositivo { get; set; }
    public string? VersionOS { get; set; }

    public DateTime DateCreated { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public decimal? Altitud { get; set; }
    public decimal? DireccionGrados { get; set; }
    public decimal? Velocidad { get; set; }
    public decimal? PrecisionVelocidad { get; set; }
    public DateTime? TimestampGps { get; set; }

    public bool? EsSimulado { get; set; }
    public string? Comentario { get; set; }
    public int? Progreso { get; set; }

    /// <summary>
    /// Imagen descargada desde UrlArchivo para incrustar en PDF.
    /// </summary>
    public byte[]? ImagenBytes { get; set; }

    /// <summary>
    /// Imagen del mapa estático generado con la ubicación GPS.
    /// </summary>
    public byte[]? MapaBytes { get; set; }

    /// <summary>
    /// Dirección enriquecida obtenida por geocodificación inversa.
    /// </summary>
    public string? DireccionFormateada { get; set; }

    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Pais { get; set; }

    /// <summary>
    /// URL de referencia para abrir la ubicación en Google Maps.
    /// </summary>
    public string? GoogleMapsUrl { get; set; }
}