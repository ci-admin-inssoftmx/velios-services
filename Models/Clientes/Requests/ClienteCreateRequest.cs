namespace velios.Api.Models.Clientes.Requests;

public class ClienteCreateRequest
{
    public string? RFC { get; set; }
    public string? RazonSocial { get; set; }
    public string? NombreComercial { get; set; }
    public string CorreoContacto { get; set; } = "";
    public string? TelefonoContacto { get; set; }
}

public class ClienteUpdateRequest : ClienteCreateRequest { }

public class ClienteProveedorNotasRequest
{
    public string? Notas { get; set; }
}

public class ProyectoCreateRequest
{
    public int ClienteId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

public class ProyectoUpdateRequest
{
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

public class AsignarProveedorProyectoRequest
{
    public int ProveedorId { get; set; }
    public int TipoServicioId { get; set; }
}

public class PresupuestoCreateRequest
{
    public int ProyectoId { get; set; }
    public int? ProveedorId { get; set; }
    public int? TipoServicioId { get; set; }
    public decimal Monto { get; set; }
    public string? Moneda { get; set; } = "MXN";
}

public class CentroTrabajoCreateRequest
{
    public int ClienteId { get; set; }
    public string Nombre { get; set; } = "";

    public string? Estado { get; set; }
    public string? Zona { get; set; }
    public string? Territorio { get; set; }
    public string? Region { get; set; }

    public string? Calle { get; set; }
    public string? Numero { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? CodigoPostal { get; set; }

    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
}

public class GeocercaRadioRequest
{
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public int RadioMetros { get; set; }
}

public class PuntoRequest
{
    public int Orden { get; set; }
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
}

public class GeocercaPoligonoRequest
{
    public List<PuntoRequest> Puntos { get; set; } = new();
}