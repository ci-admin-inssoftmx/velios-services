namespace velios.Api.Models.CodigosPostales;

public class CodigoPostalInfoDto
{
    public string CodigoPostal { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Municipio { get; set; } = "";
    public List<string> Colonias { get; set; } = new();
}