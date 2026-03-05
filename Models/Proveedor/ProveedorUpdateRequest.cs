using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class ProveedorUpdateRequest
{
    [Required] public int ProveedorId { get; set; }

    [Required] public string RazonSocial { get; set; } = "";
    public string? NombreComercial { get; set; }

    [Required] public string CorreoContacto { get; set; } = "";
    public string? TelefonoContacto { get; set; }
    public string? RepresentanteLegal { get; set; }
}