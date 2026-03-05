namespace velios.Api.Models.Auth;

public class UnidadDireccionDto
{
    public string Calle { get; set; } = "";
    public string NumeroInterior { get; set; } = "";
    public string NumeroExterior { get; set; } = "";
    public int EstadoId { get; set; }
    public string Estado { get; set; } = "";
    public int ColoniaId { get; set; }
    public string Colonia { get; set; } = "";
    public int MunicipioId { get; set; }
    public string Municipio { get; set; } = "";
    public int CodigoPostalId { get; set; }
    public string CodigoPostal { get; set; } = "";
}