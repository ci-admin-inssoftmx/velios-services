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
<<<<<<< HEAD

    public string? Calle { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Colonia { get; set; }
    public string? DelegacionMunicipio { get; set; }
    public string? Ciudad { get; set; }
    public string? Estado { get; set; }
    public string? Pais { get; set; }
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
}