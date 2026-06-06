using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("tb_CentroDeTrabajo", Schema = "dbo")]
public class NomclickCentroTrabajo
{
    [Key]
    public int IdCentroDeTrabajo { get; set; }
    public int? Estatus { get; set; }
    public string? Nombre { get; set; }
    public string? Responsable { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Telefono { get; set; }
    public int? IdCP { get; set; }
    public int? IdEmpresa { get; set; }
    public int? IdNivel { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public int? Rango { get; set; }
}