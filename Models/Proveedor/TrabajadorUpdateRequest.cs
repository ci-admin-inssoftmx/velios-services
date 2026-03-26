using System.ComponentModel.DataAnnotations;
namespace velios.Api.Models.Proveedores;

public class TrabajadorUpdateRequest
{
    [Required] public string Nombre { get; set; } = "";
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? CURP { get; set; }
    public string? RFC { get; set; }
    public string? NSS { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
    public string? TipoDeMiembro { get; set; }
    public string? Nivel { get; set; }
    public string? Clientes { get; set; }
    public string? CentroDeTrabajo { get; set; }
}