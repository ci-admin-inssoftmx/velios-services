using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class ProveedorCreateRequest
{
    [Required, MaxLength(20)]
    public string RFC { get; set; } = "";

    [Required, MaxLength(250)]
    public string RazonSocial { get; set; } = "";

    [MaxLength(250)]
    public string? NombreComercial { get; set; }

    [Required, EmailAddress, MaxLength(150)]
    public string CorreoContacto { get; set; } = "";

    [MaxLength(20)]
    public string? TelefonoContacto { get; set; }

    [MaxLength(250)]
    public string? RepresentanteLegal { get; set; }
}