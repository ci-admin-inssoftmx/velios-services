namespace velios.Api.Models.Tareas.Requests;

public class TareaUpdateRequest
{
    public List<EvidenciaFotoRequest>? EvidencePhotos { get; set; }
    public string? Observations { get; set; }
    public string? NewStatusCode { get; set; }
    public List<TimelineEventRequest>? TimelineEvents { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EvidenciaFotoRequest
{
    public string Type { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public UbicacionRequest? Location { get; set; }
    public DireccionRequest? Address { get; set; }
    public DispositivoRequest? DeviceInfo { get; set; }
}

public class UbicacionRequest
{
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public decimal? Altitude { get; set; }
    public decimal? Heading { get; set; }
    public decimal? Speed { get; set; }
    public decimal? SpeedAccuracy { get; set; }
    public DateTime? Timestamp { get; set; }
    public bool? IsMocked { get; set; }
}

public class DireccionRequest
{
    public string? FormattedAddress { get; set; }
    public string? Locality { get; set; }
    public string? AdministrativeArea { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
}

public class DispositivoRequest
{
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public string? BuildNumber { get; set; }
    public string? DeviceModel { get; set; }
    public string? OsVersion { get; set; }
}

public class TimelineEventRequest
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
}