using System.ComponentModel.DataAnnotations;
namespace velios.Api.Models.Proveedores;

public class TrabajadorCreateRequest
{
    [Required] public int ProveedorId { get; set; }
    [Required] public string Nombre { get; set; } = "";
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? CURP { get; set; }
    public string? RFC { get; set; }
    public string? NSS { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
    // --- NUEVOS CAMPOS ---
    public string? TipoDeMiembro { get; set; }   // 'Trabajador' o 'Supervisor'
    public string? Nivel { get; set; }            // 'Junior', 'Semi', 'Senior'
    public string? Clientes { get; set; }         // JSON string "[1,2,3]"
    public string? CentroDeTrabajo { get; set; }  // JSON string "[1,2,3]"
    public string? Password { get; set; }

}
