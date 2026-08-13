public enum EstadoRuta
{
    Activa = 1,
    Finalizada = 2
}

public class IniciarRutaRequest
{
    public int TareaId { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public long UsuarioId { get; set; }
}

public class FinalizarRutaRequest
{
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
}

public class RutaDto
{
    public int Id { get; set; }
    public int TareaId { get; set; }
    public EstadoRuta Estado { get; set; }
    public decimal LatitudInicio { get; set; }
    public decimal LongitudInicio { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public decimal? LatitudFin { get; set; }
    public decimal? LongitudFin { get; set; }
    public DateTime? FechaHoraFin { get; set; }
}

public class EvidenciaGeoDto
{
    public int EvidenciaId { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public DateTime DateCreated { get; set; }
    public string UrlArchivo { get; set; }
}

public class GrupoEvidenciasDto
{
    public decimal LatitudCentro { get; set; }
    public decimal LongitudCentro { get; set; }
    public int Cantidad { get; set; }
    public List<EvidenciaGeoDto> Evidencias { get; set; } = new();
}

public class ResumenRutaDto
{
    public RutaDto Ruta { get; set; }
    public List<GrupoEvidenciasDto> GruposEvidencias { get; set; } = new();
    public List<string> Observaciones { get; set; } = new();
}